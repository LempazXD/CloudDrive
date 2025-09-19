using CloudDrive.Domain.Enums;

namespace CloudDrive.Application.Interfaces;

public interface IEmailService
{
	Task SendMailCode(string email, string code);
	void PreSendMailCode(string? email, MailCodeType authCodeType);
}