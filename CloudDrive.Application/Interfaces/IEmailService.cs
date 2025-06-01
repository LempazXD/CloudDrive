namespace CloudDrive.Infrastructure.Services;

public interface IEmailService
{
	Task SendAuthCode(string email, string code);
}