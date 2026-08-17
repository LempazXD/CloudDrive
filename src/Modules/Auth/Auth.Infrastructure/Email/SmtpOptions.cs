namespace Auth.Infrastructure.Email;

public sealed class SmtpOptions
{
	public required string Host { get; init; }

	public required int Port { get; init; }

	public bool UseStartTls { get; init; } = true;

	public string? Username { get; init; }

	public string? Password { get; init; }

	public required string FromAddress { get; init; }

	public string? FromName { get; init; }
}
