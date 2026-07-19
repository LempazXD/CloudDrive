using Auth.Core.Application.Abstractions;
using Auth.Core.Domain;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Shared.Kernel.Guids;
using Shared.Kernel.Results;

namespace Auth.Infrastructure.Application;

internal sealed class AuthService(
	UserManager<ApplicationUser> userManager,
	SignInManager<ApplicationUser> signInManager,
	IJwtTokenGenerator jwtTokenGenerator,
	IRefreshTokenRepository refreshTokenRepository,
	IRefreshTokenReplayCache refreshTokenReplayCache,
	IGuidProvider guidProvider,
	TimeProvider timeProvider,
	IOptions<JwtOptions> jwtOptions,
	ILogger<AuthService> logger) : IAuthService
{
	public async Task<Result<AuthUserSummary>> RegisterAsync(
		string username,
		string email,
		string password,
		CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(username))
			return Error.Validation("Auth.User.InvalidUsername");

		if (string.IsNullOrWhiteSpace(email))
			return Error.Validation("Auth.User.InvalidEmail");

		if (string.IsNullOrWhiteSpace(password))
			return Error.Validation("Auth.User.WeakPassword");

		var user = new ApplicationUser
		{
			Id = guidProvider.CreateVersion7(),
			UserName = username,
			Email = email
		};

		try
		{
			var result = await userManager.CreateAsync(user, password);
			if (result.Succeeded)
				logger.LogInformation("User {UserId} registered with username {Username}.", user.Id, username);

			return result.ToResult(new AuthUserSummary(user.Id, username, email), "Auth.User.RegistrationFailed", logger);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException
		                                   {
			                                   SqlState: PostgresErrorCodes.UniqueViolation
		                                   } pgEx)
		{
			// UserManager.CreateAsync (RequireUniqueEmail): - check и insert не атомарны.
			// Два параллельных /register с одинаковым email могут оба пройти проверку до того, как
			// первый из них будет вставлен, и тогда второй INSERT упадёт на уникальном индексе в БД
			// (ApplicationUserConfiguration - EmailIndex/UserNameIndex), а не на штатной валидации Identity.
			// Этот catch - подстраховка на такой случай: сводит гонку к тому же Conflict, что и обычные
			// DuplicateUserName/DuplicateEmail из IdentityResultExtensions.
			switch (pgEx.ConstraintName)
			{
				case "UserNameIndex":
					logger.LogWarning(
						"Registration hit a unique-constraint race on username {Username}; Identity's pre-check passed but the database insert lost the race.",
						username);
					return Error.Conflict("Auth.User.UsernameAlreadyExists");
				case "EmailIndex":
					logger.LogWarning(
						"Registration hit a unique-constraint race on email {Email}; Identity's pre-check passed but the database insert lost the race.",
						email);
					return Error.Conflict("Auth.User.EmailAlreadyExists");
				default:
					throw;
			}
		}
	}

	public async Task<Result<AuthTokens>> LoginAsync(string login, string password, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
			return Error.Unauthorized("Auth.User.InvalidCredentials");

		var user = await userManager.FindByNameAsync(login)
		           ?? await userManager.FindByEmailAsync(login);

		if (user is null)
		{
			logger.LogWarning("Login failed for {Login}: no matching user.", login);
			return Error.Unauthorized("Auth.User.InvalidCredentials");
		}

		var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
		if (signInResult.IsLockedOut)
		{
			var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
			logger.LogWarning(
				"Login blocked for user {UserId} ({Login}): account locked out until {LockoutEndUtc}.",
				user.Id, login, lockoutEnd);
			return lockoutEnd is { } end
				? Error.LockedOut("Auth.User.LockedOut", end)
				: Error.LockedOut("Auth.User.LockedOut");
		}

		if (!signInResult.Succeeded)
		{
			logger.LogWarning("Login failed for user {UserId} ({Login}): invalid password.", user.Id, login);
			return Error.Unauthorized("Auth.User.InvalidCredentials");
		}

		return await IssueTokensAsync(user, tokenToRotate: null, ct);
	}

	public async Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(refreshToken))
			return Error.Unauthorized("Auth.RefreshToken.Invalid");

		var hash = RefreshTokenGenerator.Hash(refreshToken);
		var existing = await refreshTokenRepository.GetByTokenHashAsync(hash, ct);
		if (existing is null)
		{
			logger.LogWarning("Refresh failed: no refresh token found for the presented value.");
			return Error.Unauthorized("Auth.RefreshToken.Invalid");
		}

		var now = timeProvider.GetUtcNow();

		if (existing.IsRevoked)
		{
			// Льготное окно: если это тот же самый повтор недавно завершённой ротации (например,
			// клиент не получил ответ из-за сетевого сбоя и повторил запрос с тем же токеном),
			// отдаём ту же пару токенов вместо того, чтобы трактовать предъявление отозванного
			// токена как кражу.
			if (refreshTokenReplayCache.TryGet(hash, out var replayedTokens))
			{
				logger.LogInformation(
					"Refresh token {TokenId} replayed within grace window for user {UserId}, session {SessionId}; returning cached rotation result.",
					existing.Id, existing.UserId, existing.SessionId);
				return Result.Success(replayedTokens);
			}

			logger.LogWarning(
				"Refresh token reuse detected for user {UserId}: revoking session {SessionId} after replay of already-rotated token {TokenId}.",
				existing.UserId, existing.SessionId, existing.Id);
			await refreshTokenRepository.RevokeSessionAsync(existing.SessionId, now, ct);
			return Error.Unauthorized("Auth.RefreshToken.Revoked");
		}

		if (existing.IsExpired(now))
		{
			logger.LogWarning(
				"Refresh failed for user {UserId}, session {SessionId}: token {TokenId} expired.",
				existing.UserId, existing.SessionId, existing.Id);
			return Error.Unauthorized("Auth.RefreshToken.Expired");
		}

		var user = await userManager.FindByIdAsync(existing.UserId.ToString());
		if (user is null)
		{
			logger.LogWarning("Refresh token {TokenId} references missing user {UserId}.", existing.Id, existing.UserId);
			return Error.Unauthorized("Auth.RefreshToken.Invalid");
		}

		if (await userManager.IsLockedOutAsync(user))
		{
			var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
			logger.LogWarning(
				"Refresh blocked for user {UserId}, session {SessionId}: account locked out until {LockoutEndUtc}.",
				user.Id, existing.SessionId, lockoutEnd);
			return lockoutEnd is { } end
				? Error.LockedOut("Auth.User.LockedOut", end)
				: Error.LockedOut("Auth.User.LockedOut");
		}

		return await IssueTokensAsync(user, tokenToRotate: existing, ct);
	}

	public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(refreshToken))
			return Result.Success();

		var hash = RefreshTokenGenerator.Hash(refreshToken);
		var revoked = await refreshTokenRepository.TryRevokeByHashAsync(hash, timeProvider.GetUtcNow(), ct);
		if (revoked)
			logger.LogInformation("Refresh token revoked via logout.");

		return Result.Success();
	}

	private async Task<Result<AuthTokens>> IssueTokensAsync(
		ApplicationUser user, RefreshToken? tokenToRotate, CancellationToken ct)
	{
		var now = timeProvider.GetUtcNow();
		var newTokenId = guidProvider.CreateVersion7();
		var sessionId = tokenToRotate?.SessionId ?? newTokenId;
		var rawRefreshToken = RefreshTokenGenerator.GenerateRaw();
		var refreshTokenExpiresAtUtc = now.Add(jwtOptions.Value.RefreshTokenLifetime);
		var refreshToken = RefreshToken.Create(
			id: newTokenId,
			userId: user.Id,
			sessionId: sessionId,
			tokenHash: RefreshTokenGenerator.Hash(rawRefreshToken),
			createdAtUtc: now,
			expiresAtUtc: refreshTokenExpiresAtUtc);

		if (tokenToRotate is not null)
		{
			// Атомарный захват страхует от гонки параллельных refresh-запросов на один и тот же токен:
			// кто не успел его захватить, получает Unauthorized вместо новой пары токенов.
			var claimed = await refreshTokenRepository.TryRotateAsync(tokenToRotate.Id, refreshToken, now, ct);
			if (!claimed)
			{
				logger.LogWarning(
					"Refresh token rotation race lost for token {TokenId} (user {UserId}); another request already rotated or revoked it.",
					tokenToRotate.Id, user.Id);
				return Error.Unauthorized("Auth.RefreshToken.Revoked");
			}
		}
		else
		{
			await refreshTokenRepository.AddAsync(refreshToken, ct);
			await refreshTokenRepository.SaveChangesAsync(ct);
		}

		var (accessToken, accessTokenExpiresAtUtc) = jwtTokenGenerator.GenerateAccessToken(user);
		var tokens = new AuthTokens(accessToken, accessTokenExpiresAtUtc, rawRefreshToken, refreshTokenExpiresAtUtc);

		if (tokenToRotate is not null)
		{
			refreshTokenReplayCache.Set(tokenToRotate.TokenHash, tokens);
			logger.LogInformation(
				"Refresh token rotated for user {UserId}, session {SessionId}: {OldTokenId} -> {NewTokenId}.",
				user.Id, sessionId, tokenToRotate.Id, newTokenId);
		}
		else
		{
			logger.LogInformation("User {UserId} logged in; issued new session {SessionId}.", user.Id, sessionId);
		}

		return Result.Success(tokens);
	}
}
