namespace Auth.Core.Application.Abstractions;

public sealed record AuthUserSummary(Guid UserId, string Username, string Email);
