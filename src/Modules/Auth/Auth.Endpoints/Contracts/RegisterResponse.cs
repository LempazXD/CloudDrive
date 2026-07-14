namespace Auth.Endpoints.Contracts;

public sealed record RegisterResponse(Guid UserId, string Username, string Email);
