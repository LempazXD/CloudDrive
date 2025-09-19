using System.ComponentModel.DataAnnotations;

namespace CloudDrive.Application.Requests;

public class LoginRequest
{
	[Required(ErrorMessage = "Введите логин или email")]
	[MinLength(3, ErrorMessage = "Поле должно содержать не менее 3 символов")]
	[MaxLength(50, ErrorMessage = "Поле должно содержать не более 50 символов")]
	public string UsernameOrEmail { get; set; } = default!;
	[Required(ErrorMessage = "Введите пароль")]
	[MinLength(8, ErrorMessage = "Пароль должен содержать не менее 8 символов")]
	[MaxLength(50, ErrorMessage = "Пароль должен содержать не менее 8 символов")]
	public string Password { get; set; } = default!;
}
