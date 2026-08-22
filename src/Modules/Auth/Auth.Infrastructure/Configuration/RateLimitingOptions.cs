namespace Auth.Infrastructure.Configuration;

public sealed class RateLimitingOptions
{
	public required RateLimitRuleOptions Login { get; init; }

	public required RateLimitRuleOptions Register { get; init; }

	public required RateLimitRuleOptions ConfirmRegistration { get; init; }

	public required RateLimitRuleOptions ForgotPassword { get; init; }

	public required RateLimitRuleOptions ResetPassword { get; init; }

	public required RateLimitRuleOptions ChangePassword { get; init; }

	public required RateLimitRuleOptions ConfirmChangePassword { get; init; }
}

public sealed class RateLimitRuleOptions
{
	public required int PermitLimit { get; init; }

	public required TimeSpan Window { get; init; }
}
