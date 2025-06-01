namespace CloudDrive.Application.DTOs.Requests;

public class VerifyAuthCodeRequestDto
{
	public string LoginOrEmail { get; set; } = default!;
	public string Code { get; set; } = default!;
}
