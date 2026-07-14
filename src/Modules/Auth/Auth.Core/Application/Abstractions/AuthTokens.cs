namespace Auth.Core.Application.Abstractions;

public sealed record AuthTokens(
	string AccessToken,
	DateTimeOffset AccessTokenExpiresAtUtc,
	string RefreshToken,
	DateTimeOffset RefreshTokenExpiresAtUtc);
