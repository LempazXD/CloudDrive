namespace Auth.Infrastructure.Configuration;

public sealed class RegistrationOptions
{
	public TimeSpan CodeLifetime { get; init; } = TimeSpan.FromMinutes(15);

	public int MaxAttempts { get; init; } = 5;
}
