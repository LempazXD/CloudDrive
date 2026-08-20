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
	IPendingRegistrationRepository pendingRegistrationRepository,
	IEmailSender emailSender,
	IGuidProvider guidProvider,
	TimeProvider timeProvider,
	IOptions<JwtOptions> jwtOptions,
	IOptions<RegistrationOptions> registrationOptions,
	ILogger<AuthService> logger) : IAuthService
{
	public async Task<Result<VerificationCodeSent>> RegisterAsync(
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

		// Транзитный пользователь только для валидаторов Identity; никогда не сохраняется.
		var transientUser = new ApplicationUser { UserName = username, Email = email };
		var validation = await ValidateTransientUserAsync(transientUser, password);

		var now = timeProvider.GetUtcNow();
		var codeExpiresAtUtc = now.Add(registrationOptions.Value.CodeLifetime);
		var response = new VerificationCodeSent(email, codeExpiresAtUtc);

		if (!validation.Succeeded)
			return validation.ToResult(response, "Auth.User.RegistrationFailed", logger);

		var passwordHash = userManager.PasswordHasher.HashPassword(transientUser, password);
		var rawCode = VerificationCodeGenerator.GenerateRaw();
		var normalizedEmail = userManager.NormalizeEmail(email)!;

		await pendingRegistrationRepository.DeleteExpiredAsync(now, ct);

		var existingPending = await pendingRegistrationRepository.GetByNormalizedEmailAsync(normalizedEmail, ct);
		if (existingPending is not null)
			await pendingRegistrationRepository.RemoveAsync(existingPending, ct);

		var pending = PendingRegistration.Create(
			guidProvider.CreateVersion7(),
			normalizedEmail,
			email,
			username,
			passwordHash,
			VerificationCodeGenerator.Hash(rawCode),
			now,
			codeExpiresAtUtc);

		try
		{
			await pendingRegistrationRepository.AddAsync(pending, ct);
			await pendingRegistrationRepository.SaveChangesAsync(ct);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException
		                                   {
			                                   SqlState: PostgresErrorCodes.UniqueViolation
		                                   } pgEx)
		{
			// Гонка двух параллельных /register с одинаковым email до его подтверждения - тот же
			// Conflict, что и для уже существующего аккаунта: клиенту в обоих случаях остаётся
			// только повторить попытку.
			if (pgEx.ConstraintName != "PendingRegistrationEmailIndex")
				throw;

			logger.LogWarning("Registration initiation hit a unique-constraint race on email {Email}.", email);
			return Error.Conflict("Auth.User.EmailAlreadyExists");
		}

		await emailSender.SendRegistrationCodeAsync(email, rawCode, registrationOptions.Value.CodeLifetime, ct);
		logger.LogInformation("Registration code issued for {Email}.", email);

		return Result.Success(response);
	}

	public async Task<Result<AuthTokens>> ConfirmRegistrationAsync(string email, string code, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
			return Error.NotFound("Auth.RegistrationCode.NotFound");

		var normalizedEmail = userManager.NormalizeEmail(email)!;
		var pending = await pendingRegistrationRepository.GetByNormalizedEmailAsync(normalizedEmail, ct);
		if (pending is null)
		{
			logger.LogWarning("Registration confirmation failed for {Email}: no pending registration found.", email);
			return Error.NotFound("Auth.RegistrationCode.NotFound");
		}

		var now = timeProvider.GetUtcNow();
		if (pending.IsExpired(now))
		{
			await pendingRegistrationRepository.RemoveAsync(pending, ct);
			await pendingRegistrationRepository.SaveChangesAsync(ct);
			logger.LogWarning("Registration confirmation failed for {Email}: code expired.", email);
			return Error.Unauthorized("Auth.RegistrationCode.Expired");
		}

		if (VerificationCodeGenerator.Hash(code) != pending.CodeHash)
		{
			pending.RecordFailedAttempt();

			if (pending.HasExceededAttempts(registrationOptions.Value.MaxAttempts))
			{
				await pendingRegistrationRepository.RemoveAsync(pending, ct);
				await pendingRegistrationRepository.SaveChangesAsync(ct);
				logger.LogWarning("Registration confirmation for {Email} exceeded the maximum attempt count.", email);
				return Error.Validation("Auth.RegistrationCode.TooManyAttempts");
			}

			await pendingRegistrationRepository.SaveChangesAsync(ct);
			logger.LogWarning(
				"Registration confirmation failed for {Email}: incorrect code (attempt {AttemptCount}).",
				email, pending.AttemptCount);
			return Error.Unauthorized("Auth.RegistrationCode.Invalid");
		}

		var user = new ApplicationUser
		{
			Id = guidProvider.CreateVersion7(),
			UserName = pending.Username,
			Email = pending.Email,
			EmailConfirmed = true,
			PasswordHash = pending.PasswordHash
		};

		IdentityResult createResult;
		try
		{
			// Однопараметровый CreateAsync: валидирует (свежая проверка уникальности) и персистит,
			// но не хэширует пароль повторно - PasswordHash уже посчитан на этапе RegisterAsync.
			createResult = await userManager.CreateAsync(user);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException
		                                   {
			                                   SqlState: PostgresErrorCodes.UniqueViolation
		                                   } pgEx)
		{
			switch (pgEx.ConstraintName)
			{
				case "UserNameIndex":
					logger.LogWarning(
						"Registration confirmation hit a unique-constraint race on username {Username}.",
						pending.Username);
					return Error.Conflict("Auth.User.UsernameAlreadyExists");
				case "EmailIndex":
					logger.LogWarning(
						"Registration confirmation hit a unique-constraint race on email {Email}.", pending.Email);
					return Error.Conflict("Auth.User.EmailAlreadyExists");
				default:
					throw;
			}
		}

		if (!createResult.Succeeded)
			return createResult.ToResult("Auth.User.RegistrationFailed", logger).Error!;

		await pendingRegistrationRepository.RemoveAsync(pending, ct);
		await pendingRegistrationRepository.SaveChangesAsync(ct);

		logger.LogInformation(
			"User {UserId} completed registration via confirmation code for {Email}.", user.Id, email);

		return await IssueTokensAsync(user, tokenToRotate: null, ct);
	}

	private async Task<IdentityResult> ValidateTransientUserAsync(ApplicationUser transientUser, string password)
	{
		var errors = new List<IdentityError>();

		// Пароль перед пользователем - тот же порядок, что и внутри UserManager.CreateAsync(user, password)
		// сегодня (UpdatePasswordHash до CreateAsync(user)), чтобы приоритет ошибок при нескольких
		// одновременных нарушениях не менялся по сравнению с прежним поведением.
		foreach (var validator in userManager.PasswordValidators)
		{
			var result = await validator.ValidateAsync(userManager, transientUser, password);
			if (!result.Succeeded)
				errors.AddRange(result.Errors);
		}

		foreach (var validator in userManager.UserValidators)
		{
			var result = await validator.ValidateAsync(userManager, transientUser);
			if (!result.Succeeded)
				errors.AddRange(result.Errors);
		}

		return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
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
			var (error, lockoutEnd) = await BuildLockedOutErrorAsync(user);
			logger.LogWarning(
				"Login blocked for user {UserId} ({Login}): account locked out until {LockoutEndUtc}.",
				user.Id, login, lockoutEnd);
			return error;
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
			var (error, lockoutEnd) = await BuildLockedOutErrorAsync(user);
			logger.LogWarning(
				"Refresh blocked for user {UserId}, session {SessionId}: account locked out until {LockoutEndUtc}.",
				user.Id, existing.SessionId, lockoutEnd);
			return error;
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

	public async Task<Result> LogoutAllAsync(string refreshToken, bool keepCurrentSession, CancellationToken ct)
	{
		// В отличие от LogoutAsync, здесь нельзя молча вернуть успех для нераспознанного токена:
		// без него невозможно определить, чей именно аккаунт разлогинивать.
		if (string.IsNullOrWhiteSpace(refreshToken))
			return Result.Failure(Error.Unauthorized("Auth.RefreshToken.Invalid"));

		var hash = RefreshTokenGenerator.Hash(refreshToken);
		var existing = await refreshTokenRepository.GetByTokenHashAsync(hash, ct);
		if (existing is null)
		{
			logger.LogWarning("Logout-all failed: no refresh token found for the presented value.");
			return Result.Failure(Error.Unauthorized("Auth.RefreshToken.Invalid"));
		}

		var now = timeProvider.GetUtcNow();

		if (existing.IsRevoked)
		{
			logger.LogWarning(
				"Logout-all failed for user {UserId}, session {SessionId}: token {TokenId} already revoked.",
				existing.UserId, existing.SessionId, existing.Id);
			return Result.Failure(Error.Unauthorized("Auth.RefreshToken.Revoked"));
		}

		if (existing.IsExpired(now))
		{
			logger.LogWarning(
				"Logout-all failed for user {UserId}, session {SessionId}: token {TokenId} expired.",
				existing.UserId, existing.SessionId, existing.Id);
			return Result.Failure(Error.Unauthorized("Auth.RefreshToken.Expired"));
		}

		if (keepCurrentSession)
		{
			await refreshTokenRepository.RevokeAllForUserExceptSessionAsync(existing.UserId, existing.SessionId, now, ct);
			logger.LogInformation(
				"User {UserId} logged out of all sessions except the current one ({SessionId}).",
				existing.UserId, existing.SessionId);
		}
		else
		{
			await refreshTokenRepository.RevokeAllForUserAsync(existing.UserId, now, ct);
			logger.LogInformation("User {UserId} logged out of all sessions, including the current one.", existing.UserId);
		}

		return Result.Success();
	}

	private async Task<(Error Error, DateTimeOffset? LockoutEndUtc)> BuildLockedOutErrorAsync(ApplicationUser user)
	{
		var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
		var error = lockoutEnd is { } end
			? Error.LockedOut("Auth.User.LockedOut", end)
			: Error.LockedOut("Auth.User.LockedOut");
		return (error, lockoutEnd);
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
