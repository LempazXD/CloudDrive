using System.ComponentModel.DataAnnotations;

namespace CloudDrive.Application.Requests;

public class VerifyCodeRequestDto
{
	[Required(ErrorMessage = "Введите email")]
	[MinLength(3, ErrorMessage = "Поле должно содержать не менее 3 символов")]
	[MaxLength(50, ErrorMessage = "Поле должно содержать не более 50 символов")]
	public string Email { get; set; } = default!;
	[Required(ErrorMessage = "Введите код")]
	[Length(6, 6, ErrorMessage = "Код должен содержать 6 символов")]
	public string Code { get; set; } = default!;
}
