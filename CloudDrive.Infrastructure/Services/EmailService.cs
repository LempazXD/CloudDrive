using CloudDrive.Application.Interfaces;
using CloudDrive.Domain.Entities;
using CloudDrive.Domain.Enums;
using CloudDrive.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

namespace CloudDrive.Infrastructure.Services;

public class EmailService : IEmailService
{
	private readonly IMailCodeRepository _authCodeRep;
	private readonly IConfiguration _config;
	private readonly string _senderEmail;
	private readonly string _senderPassword;
	private readonly string _smtpServer;
	private readonly int _port;

	private const string _mailCodeSymbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
	private const int _mailCodeLength = 6;

	public EmailService(IConfiguration config, IMailCodeRepository authCodeRep)
	{
		_config = config;
		_authCodeRep = authCodeRep;

		_senderEmail = _config["Email:SenderEmail"]!;
		_senderPassword = _config["Email:SenderPassword"]!;
		_smtpServer = _config["Email:SmtpServer"]!;
		_port = int.Parse(_config["Email:Port"]!);
	}

	public async void PreSendMailCode(string? email, MailCodeType authCodeType) // !!! Мб изменить название?
	{
		var newCode = GenerateMailCode();
		var authCode = await _authCodeRep.FindByEmail(email); // !!! Если email null, всё будет норм?
		var now = DateTime.UtcNow;

		if (authCode == null)
		{
			//!!!!!!!!!!!!!!!!!!!!!!!!!authCode = new MailCodeEntity(email, newCode, now, authCodeType);
			await _authCodeRep.Add(authCode);
		}
		else
		{
			var delay = GetMailCodeDelay(authCode.SentCodeCount);
			var nextAvailableTime = authCode.CreatedAt.ToUniversalTime().AddMinutes(delay);

			if (now < nextAvailableTime)
			{
				var secondsLeft = Math.Ceiling((nextAvailableTime - now).TotalSeconds);
				throw new Exception($"Подождите {secondsLeft} секунд перед отправкой нового кода");
			}

			authCode.NewCode(newCode);

			_authCodeRep.Update(authCode);
		}
		await _authCodeRep.SaveChanges();

		await SendMailCode(email, newCode);
	}

	public async Task SendMailCode(string email, string code)
	{
		try
		{
			var fromAddress = new MailAddress(_senderEmail!, "CloudDrive");
			var toAddress = new MailAddress(email);

			using var smtp = new SmtpClient
			{
				Host = _smtpServer,
				Port = _port,
				EnableSsl = true,
				DeliveryMethod = SmtpDeliveryMethod.Network,
				UseDefaultCredentials = false,
				Credentials = new NetworkCredential(fromAddress.Address, _senderPassword),
				Timeout = 10000 // 10 сек
			};

			using var message = new MailMessage(fromAddress, toAddress)
			{   // !!! В другое место
				Subject = "Подтверждение email",
				Body = $@"
                        <html>
                        <body style='margin: 0; padding: 0; background-color: #f4f4f4; font-family: Arial, sans-serif;'>
                            <table width='100%' cellpadding='0' cellspacing='0'>
                                <tr>
                                    <td align='center' style='padding: 20px 0;'>
                                        <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 8px; padding: 30px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);'>
                                            <tr>
                                                <td align='center' style='padding-bottom: 20px;'>
                                                    <h2 style='color: #4CAF50; margin: 0;'>Подтверждение Email</h2>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='font-size: 16px; color: #333333; padding-bottom: 10px;'>
                                                    Здравствуйте!
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='font-size: 16px; color: #333333; padding-bottom: 20px;'>
                                                    Вы запросили код подтверждения для вашего аккаунта. Пожалуйста, используйте следующий код:
                                                </td>
                                            </tr>
                                            <tr>
                                                <td align='center' style='font-size: 28px; font-weight: bold; color: #4CAF50; padding: 20px 0; background-color: #f0fdf4; border-radius: 6px;'>
                                                    {code}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='font-size: 14px; color: #666666; padding-top: 20px;'>
                                                    Если вы не запрашивали этот код, просто проигнорируйте это письмо.
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='font-size: 14px; color: #999999; padding-top: 10px;'>
                                                    С уважением, команда CloudDrive
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </body>
                        </html>
                    ",
				IsBodyHtml = true
			};

			await smtp.SendMailAsync(message);
		}
		catch (SmtpException ex)
		{
			throw new Exception("Failed to send email due to SMTP error", ex);
		}
		catch (Exception ex)
		{
			throw new Exception("Failed to send email", ex);
		}
	}

	private string GenerateMailCode()
	{
		var code = new char[_mailCodeLength];

		using var rng = RandomNumberGenerator.Create();
		var randomBytes = new byte[_mailCodeLength];

		rng.GetBytes(randomBytes);

		for (int i = 0; i < _mailCodeLength; i++)
		{
			int index = randomBytes[i] % _mailCodeSymbols.Length;
			code[i] = _mailCodeSymbols[index];
		}

		return new string(code);
	}

	private int GetMailCodeDelay(int sentCodeCount)
	{
		if (sentCodeCount <= 1)
			return 1;
		else
			return Math.Min((int)Math.Pow(2, sentCodeCount - 1), 30);
	}
}
