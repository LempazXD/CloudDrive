using System.ComponentModel.DataAnnotations;

namespace CloudDrive.Application.Requests;

public class SendRegisterCodeRequest
{
	[Required(ErrorMessage = "Введите логин")]
	[MinLength(3, ErrorMessage = "Логин должен быть не менее 3 символов")]
	[MaxLength(50, ErrorMessage = "Логин должен быть не более 50 символов")]
	public string Username { get; set; } = default!;
	[Required(ErrorMessage = "Введите email")]
	[EmailAddress(ErrorMessage = "Неверный формат email")]
	[MinLength(5, ErrorMessage = "Email долежн быть не менее 5 символов")]
	[MaxLength(50, ErrorMessage = "Email долежн быть не более 50 символов")]
	public string Email { get; set; } = default!;
	[Required(ErrorMessage = "Введите пароль")]
	[MinLength(8, ErrorMessage = "Пароль должен быть не менее 8 символов")]
	[MaxLength(50, ErrorMessage = "Пароль должен быть не более 50 символов")]
	public string Password { get; set; } = default!;
	[Required(ErrorMessage = "Подтвердите пароль")]
	[Compare("Password", ErrorMessage = "Пароли не совпадают")]
	[MinLength(8, ErrorMessage = "Подтверждение пароля должно быть не менее 8 символов")]
	[MaxLength(50, ErrorMessage = "Подтверждение пароля должно быть не более 50 символов")]
	public string ConfirmPassword { get; set; } = default!;
}
