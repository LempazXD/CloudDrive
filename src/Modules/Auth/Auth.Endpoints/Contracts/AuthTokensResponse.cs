namespace Auth.Endpoints.Contracts;

public sealed record AuthTokensResponse(
	string AccessToken,
	DateTimeOffset AccessTokenExpiresAtUtc,
	string RefreshToken,
	DateTimeOffset RefreshTokenExpiresAtUtc);
