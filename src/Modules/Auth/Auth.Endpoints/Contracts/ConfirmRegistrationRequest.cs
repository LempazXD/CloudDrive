namespace Auth.Endpoints.Contracts;

public sealed record ConfirmRegistrationRequest(string Email, string Code);
