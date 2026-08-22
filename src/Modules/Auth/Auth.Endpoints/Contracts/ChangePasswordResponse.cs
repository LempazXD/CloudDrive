namespace Auth.Endpoints.Contracts;

public sealed record ChangePasswordResponse(DateTimeOffset CodeExpiresAtUtc);
