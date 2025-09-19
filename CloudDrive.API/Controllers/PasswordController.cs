using CloudDrive.Application.Interfaces;
using CloudDrive.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudDrive.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PasswordController : ControllerBase // !!! Обновить пространства имён во всех файлах
{

	private readonly IAuthService _authService;

	public PasswordController(IAuthService authService)
	{
		_authService = authService;
	}

	[AllowAnonymous]
	[HttpPost]
	public async Task<IActionResult> SendPasswordCode(SendPasswordCodeRequest request) // !!! Переименовать, метод для отправки кода при восстановлении пароля
	{
		return Ok();
	}

	[HttpPost]
	public async Task<IActionResult> Reset(ResetCodeRequest request) // !!! Задание нового пароля (при восстановлении (или при смене тоже?)) // Reset или ResetPassword?
	{
		return Ok();
	}

	[Authorize]
	[HttpPost]
	public async Task<IActionResult> Change(ChangeRequest request) // !!! Смена пароля (уже залогинен)   // Change или ChangePassword
	{
		return Ok();
	}
}
