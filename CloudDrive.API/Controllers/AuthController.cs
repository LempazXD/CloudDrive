using CloudDrive.Application.Interfaces;
using CloudDrive.Application.Requests;
using CloudDrive.Application.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudDrive.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
	private readonly IAuthService _authService;

	public AuthController(IAuthService authService)
	{
		_authService = authService;
	}

	[AllowAnonymous]
	[HttpPost]
	public async Task<IActionResult> Login(LoginRequest request)
	{
		var token = await _authService.Login(request);
		return Ok(new TokenResponse { Token = token }); // !!! Переименовать TokenResponse
	}

	[AllowAnonymous]
	[HttpPost]
	public async Task<IActionResult> MailCodeLogin(MailCodeLoginRequest request)
	{
		var token = await _authService.LoginAuthCode(request);
		return Ok(new TokenResponse { Token = token });
	}

	[Authorize]
	[HttpPost]
	public async Task<IActionResult> Logout(LogoutRequest request)
	{
		return Ok();
	}

	// [Атрибут поставить бы]
	[HttpPost]
	public async Task<IActionResult> Refresh(RefreshRequest request) // !!! Refresh или RefreshToken?
	{
		return Ok();
	}

}
