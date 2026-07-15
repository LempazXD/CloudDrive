namespace Auth.Infrastructure.Configuration;

public sealed class RateLimitingOptions
{
	public required RateLimitRuleOptions Login { get; init; }

	public required RateLimitRuleOptions Register { get; init; }
}

public sealed class RateLimitRuleOptions
{
	public required int PermitLimit { get; init; }

	public required TimeSpan Window { get; init; }
}
