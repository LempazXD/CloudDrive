using System.Security.Claims;
using Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Guids;

namespace Auth.Infrastructure.Identity;

internal sealed class JwtTokenGenerator(
	IOptions<JwtOptions> jwtOptions,
	TimeProvider timeProvider,
	IGuidProvider guidProvider) : IJwtTokenGenerator
{
	private static readonly JsonWebTokenHandler Handler = new();

	// JwtTokenGenerator - синглтон, а ключ подписи не меняется во время работы приложения,
	// поэтому декодируем его и строим signing credentials один раз, а не при каждом вызове GenerateAccessToken.
	private readonly SigningCredentials _signingCredentials = new(
		jwtOptions.Value.GetSecurityKey(),
		SecurityAlgorithms.HmacSha256);

	public (string Token, DateTimeOffset ExpiresAtUtc) GenerateAccessToken(ApplicationUser user)
	{
		var options = jwtOptions.Value;
		var now = timeProvider.GetUtcNow();
		var expiresAtUtc = now.Add(options.AccessTokenLifetime);

		var descriptor = new SecurityTokenDescriptor
		{
			Issuer = options.Issuer,
			Audience = options.Audience,
			Subject = new ClaimsIdentity(
			[
				new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
				// Email гарантированно не null: RegisterAsync всегда его валидирует и присваивает при создании пользователя.
				new Claim(JwtRegisteredClaimNames.Email, user.Email!),
				new Claim(JwtRegisteredClaimNames.Jti, guidProvider.CreateVersion7().ToString())
			]),
			NotBefore = now.UtcDateTime,
			IssuedAt = now.UtcDateTime,
			Expires = expiresAtUtc.UtcDateTime,
			SigningCredentials = _signingCredentials
		};

		return (Handler.CreateToken(descriptor), expiresAtUtc);
	}
}
