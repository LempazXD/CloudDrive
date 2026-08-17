namespace Auth.Core.Application.Abstractions;

public sealed record RegistrationCodeSent(string Email, DateTimeOffset CodeExpiresAtUtc);
