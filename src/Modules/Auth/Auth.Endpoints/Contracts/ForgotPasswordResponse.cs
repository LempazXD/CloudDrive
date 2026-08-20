namespace Auth.Endpoints.Contracts;

public sealed record ForgotPasswordResponse(string Email, DateTimeOffset CodeExpiresAtUtc);
