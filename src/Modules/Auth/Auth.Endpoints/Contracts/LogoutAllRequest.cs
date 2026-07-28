namespace Auth.Endpoints.Contracts;

public sealed record LogoutAllRequest(string RefreshToken, bool KeepCurrentSession);
