namespace Auth.Core.Application.Abstractions;

public static class AuthRateLimitPolicies
{
	public const string Login = "auth-login";

	public const string Register = "auth-register";

	public const string ConfirmRegistration = "auth-confirm-registration";

	public const string ForgotPassword = "auth-forgot-password";

	public const string ResetPassword = "auth-reset-password";

	public const string ChangePassword = "auth-change-password";

	public const string ConfirmChangePassword = "auth-confirm-change-password";
}
