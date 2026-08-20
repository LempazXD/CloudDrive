using System.Globalization;
using Auth.Core.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Auth.Infrastructure.Email;

// Текст письма локализован здесь, а не через JSON-локализатор ошибок (CloudDrive.Api/Shared.Api) -
// Infrastructure не может ссылаться на Shared.Api (см. таблицу модулей в корневом CLAUDE.md).
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<SmtpEmailSender> logger) : IEmailSender
{
	public async Task SendRegistrationCodeAsync(string email, string code, TimeSpan codeLifetime, CancellationToken ct)
	{
		var options = smtpOptions.Value;
		var (subject, body) = BuildRegistrationCodeMessage(code, codeLifetime);

		var message = new MimeMessage();
		message.From.Add(new MailboxAddress(options.FromName ?? options.FromAddress, options.FromAddress));
		message.To.Add(MailboxAddress.Parse(email));
		message.Subject = subject;
		message.Body = new TextPart("plain") { Text = body };

		using var client = new SmtpClient();

		// StartTlsWhenAvailable: использует STARTTLS, только если сервер его заявляет в EHLO -
		// безопасно и для локального Mailpit (TLS не поддерживает, просто продолжает без него),
		// и для реального провайдера (обычно поддерживает).
		await client.ConnectAsync(
			options.Host,
			options.Port,
			options.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
			ct);

		if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
			await client.AuthenticateAsync(options.Username, options.Password, ct);

		await client.SendAsync(message, ct);
		await client.DisconnectAsync(true, ct);

		logger.LogInformation("Registration code email sent to {Email}.", email);
	}

	public async Task SendPasswordResetCodeAsync(string email, string code, TimeSpan codeLifetime, CancellationToken ct)
	{
		var options = smtpOptions.Value;
		var (subject, body) = BuildPasswordResetMessage(code, codeLifetime);

		var message = new MimeMessage();
		message.From.Add(new MailboxAddress(options.FromName ?? options.FromAddress, options.FromAddress));
		message.To.Add(MailboxAddress.Parse(email));
		message.Subject = subject;
		message.Body = new TextPart("plain") { Text = body };

		using var client = new SmtpClient();

		await client.ConnectAsync(
			options.Host,
			options.Port,
			options.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
			ct);

		if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
			await client.AuthenticateAsync(options.Username, options.Password, ct);

		await client.SendAsync(message, ct);
		await client.DisconnectAsync(true, ct);

		logger.LogInformation("Password reset code email sent to {Email}.", email);
	}

	private static (string Subject, string Body) BuildRegistrationCodeMessage(string code, TimeSpan codeLifetime)
	{
		var minutes = (int)codeLifetime.TotalMinutes;

		return CultureInfo.CurrentUICulture.Name == "ru"
			? ($"Код подтверждения CloudDrive: {code}",
				$"""
				 Ваш код подтверждения регистрации: {code}

				 Код действителен {minutes} мин. Если вы не запрашивали регистрацию, просто проигнорируйте это письмо.
				 """)
			: ($"Your CloudDrive confirmation code: {code}",
				$"""
				 Your registration confirmation code: {code}

				 The code is valid for {minutes} min. If you did not request this, please ignore this email.
				 """);
	}

	private static (string Subject, string Body) BuildPasswordResetMessage(string code, TimeSpan codeLifetime)
	{
		var minutes = (int)codeLifetime.TotalMinutes;

		return CultureInfo.CurrentUICulture.Name == "ru"
			? ($"Код восстановления пароля CloudDrive: {code}",
				$"""
				 Ваш код для восстановления пароля: {code}

				 Код действителен {minutes} мин. Если вы не запрашивали восстановление пароля, просто проигнорируйте это письмо.
				 """)
			: ($"Your CloudDrive password reset code: {code}",
				$"""
				 Your password reset code: {code}

				 The code is valid for {minutes} min. If you did not request this, please ignore this email.
				 """);
	}
}
