using CloudDrive.Application.DTOs.Requests;

namespace CloudDrive.Application.Interfaces;

public interface IAuthService
{

	Task Register(RegisterRequestDto request);
	Task<string> Login(LoginRequestDto request);
	Task LoginAuthCode(LoginAuthCodeRequestDto request);
	Task<string?> VerifyAuthCode(VerifyAuthCodeRequestDto request);
}
