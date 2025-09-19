using CloudDrive.Application.Requests;

namespace CloudDrive.Application.Interfaces;

public interface IAuthService
{
	Task SendRegisterCode(SendRegisterCodeRequest request);
	Task<string> Register(RegisterRequest request);
	Task<string> Login(LoginRequest request);
	Task<string> LoginAuthCode(MailCodeLoginRequest request);
	Task<string?> VerifyMailCode(string usernameOrEmail, string code);
}
