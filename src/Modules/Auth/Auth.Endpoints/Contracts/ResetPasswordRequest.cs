namespace Auth.Endpoints.Contracts;

public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword, string ConfirmNewPassword);
