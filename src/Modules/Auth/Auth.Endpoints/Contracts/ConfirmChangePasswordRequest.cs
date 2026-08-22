namespace Auth.Endpoints.Contracts;

public sealed record ConfirmChangePasswordRequest(string Code, string NewPassword, string ConfirmNewPassword);
