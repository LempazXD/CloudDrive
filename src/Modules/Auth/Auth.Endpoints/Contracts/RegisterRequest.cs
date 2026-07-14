namespace Auth.Endpoints.Contracts;

public sealed record RegisterRequest(string Username, string Email, string Password);
