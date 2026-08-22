namespace Auth.Core.Application.Abstractions;

public interface IEmailSender
{
	Task SendRegistrationCodeAsync(string email, string code, TimeSpan codeLifetime, CancellationToken ct);

	Task SendPasswordResetCodeAsync(string email, string code, TimeSpan codeLifetime, CancellationToken ct);

	Task SendPasswordChangeCodeAsync(string email, string code, TimeSpan codeLifetime, CancellationToken ct);
}
