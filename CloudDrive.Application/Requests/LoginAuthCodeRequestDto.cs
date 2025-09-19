using System.ComponentModel.DataAnnotations;

namespace CloudDrive.Application.Requests;

public class LoginAuthCodeRequestDto
{
	[Required(ErrorMessage = "Введите логин или email")]
	[MinLength(3, ErrorMessage = "Поле должно содержать не менее 3 символов")]
	[MaxLength(50, ErrorMessage = "Поле должно содержать не более 50 символов")]
	public string UsernameOrEmail { get; set; } = default!;
}
