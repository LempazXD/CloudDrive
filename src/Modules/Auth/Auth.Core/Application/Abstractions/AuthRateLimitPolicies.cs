namespace Auth.Core.Application.Abstractions;

public static class AuthRateLimitPolicies
{
	public const string Login = "auth-login";

	public const string Register = "auth-register";

	public const string ConfirmRegistration = "auth-confirm-registration";
}
