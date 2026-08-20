namespace Auth.Core.Application.Abstractions;

public sealed record VerificationCodeSent(string Email, DateTimeOffset CodeExpiresAtUtc);
