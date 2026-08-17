namespace Auth.Endpoints.Contracts;

public sealed record RegisterResponse(string Email, DateTimeOffset CodeExpiresAtUtc);
