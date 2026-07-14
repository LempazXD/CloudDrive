using Auth.Core.Application.Abstractions;
using Auth.Core.Domain;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Shared.Kernel.Guids;
using Shared.Kernel.Results;

namespace Auth.Infrastructure.Application;

internal sealed class AuthService(
	UserManager<ApplicationUser> userManager,
	SignInManager<ApplicationUser> signInManager,
	IPasswordHasher<ApplicationUser> passwordHasher,
	IJwtTokenGenerator jwtTokenGenerator,
	IRefreshTokenRepository refreshTokenRepository,
	IGuidProvider guidProvider,
	TimeProvider timeProvider,
	IOptions<JwtOptions> jwtOptions) : IAuthService
{
	// Лениво считается через внедрённый хешер (та же стоимость, что и у реальной проверки),
	// чтобы ответ на неизвестный email не отличался по времени от ответа на неверный пароль и
	// не выдавал факт регистрации через тайминг;
	// Lazy<string> гарантирует однократное вычисление при параллельных первых запросах.
	private static Lazy<string>? _dummyPasswordHash;

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
			return result.ToResult(new AuthUserSummary(user.Id, username, email), "Auth.User.RegistrationFailed");
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException
		                                   {
			                                   SqlState: PostgresErrorCodes.UniqueViolation
		                                   } pgEx)
		{
			return pgEx.ConstraintName == "UserNameIndex"
				? Error.Conflict("Auth.User.UsernameAlreadyExists")
				: Error.Conflict("Auth.User.EmailAlreadyExists");
		}
	}

	public async Task<Result<AuthTokens>> LoginAsync(string login, string password, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
			return Error.Unauthorized("Auth.User.InvalidCredentials");

		var user = await userManager.FindByNameAsync(login) ?? await userManager.FindByEmailAsync(login);
		if (user is null)
		{
			var dummyHash = LazyInitializer.EnsureInitialized(
				ref _dummyPasswordHash,
				() => new Lazy<string>(() => passwordHasher.HashPassword(null!, "not-a-real-password")));
			passwordHasher.VerifyHashedPassword(null!, dummyHash.Value, password);
			return Error.Unauthorized("Auth.User.InvalidCredentials");
		}

		var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
		if (signInResult.IsLockedOut)
		{
			var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
			return lockoutEnd is { } end
				? Error.LockedOut("Auth.User.LockedOut", end)
				: Error.LockedOut("Auth.User.LockedOut");
		}

		if (!signInResult.Succeeded)
			return Error.Unauthorized("Auth.User.InvalidCredentials");

		return await IssueTokensAsync(user, tokenToRotate: null, ct);
	}

	public async Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(refreshToken))
			return Error.Unauthorized("Auth.RefreshToken.Invalid");

		var hash = RefreshTokenGenerator.Hash(refreshToken);
		var existing = await refreshTokenRepository.GetByTokenHashAsync(hash, ct);
		if (existing is null)
			return Error.Unauthorized("Auth.RefreshToken.Invalid");

		var now = timeProvider.GetUtcNow();

		if (existing.IsRevoked)
		{
			await refreshTokenRepository.RevokeAllForUserAsync(existing.UserId, now, ct);
			return Error.Unauthorized("Auth.RefreshToken.Revoked");
		}

		if (existing.IsExpired(now))
			return Error.Unauthorized("Auth.RefreshToken.Expired");

		var user = await userManager.FindByIdAsync(existing.UserId.ToString());
		if (user is null)
			return Error.Unauthorized("Auth.RefreshToken.Invalid");

		return await IssueTokensAsync(user, tokenToRotate: existing, ct);
	}

	public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(refreshToken))
			return Result.Success();

		var hash = RefreshTokenGenerator.Hash(refreshToken);
		await refreshTokenRepository.TryRevokeByHashAsync(hash, timeProvider.GetUtcNow(), ct);

		return Result.Success();
	}

	private async Task<Result<AuthTokens>> IssueTokensAsync(
		ApplicationUser user, RefreshToken? tokenToRotate, CancellationToken ct)
	{
		var now = timeProvider.GetUtcNow();
		var newTokenId = guidProvider.CreateVersion7();
		var rawRefreshToken = RefreshTokenGenerator.GenerateRaw();
		var refreshTokenExpiresAtUtc = now.Add(jwtOptions.Value.RefreshTokenLifetime);
		var refreshToken = RefreshToken.Create(
			newTokenId,
			user.Id,
			RefreshTokenGenerator.Hash(rawRefreshToken),
			now,
			refreshTokenExpiresAtUtc);

		if (tokenToRotate is not null)
		{
			// Атомарный захват страхует от гонки параллельных refresh-запросов на один и тот же токен:
			// кто не успел его захватить, получает Unauthorized вместо новой пары токенов.
			var claimed = await refreshTokenRepository.TryRotateAsync(tokenToRotate.Id, refreshToken, now, ct);
			if (!claimed)
				return Error.Unauthorized("Auth.RefreshToken.Revoked");
		}
		else
		{
			await refreshTokenRepository.AddAsync(refreshToken, ct);
			await refreshTokenRepository.SaveChangesAsync(ct);
		}

		var (accessToken, accessTokenExpiresAtUtc) = jwtTokenGenerator.GenerateAccessToken(user);
		return Result.Success(new AuthTokens(accessToken, accessTokenExpiresAtUtc, rawRefreshToken,
			refreshTokenExpiresAtUtc));
	}
}
