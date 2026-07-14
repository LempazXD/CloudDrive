namespace Auth.Infrastructure.Identity;

internal interface IJwtTokenGenerator
{
	(string Token, DateTimeOffset ExpiresAtUtc) GenerateAccessToken(ApplicationUser user);
}
