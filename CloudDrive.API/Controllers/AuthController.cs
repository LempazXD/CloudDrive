using CloudDrive.Application.DTOs.Requests;
using CloudDrive.Application.Interfaces;
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

	[HttpPost("register")]
	public async Task<IActionResult> Register(RegisterRequestDto request)
	{
		await _authService.Register(request);
		return Ok();
	}

	[HttpPost("login")]
	public async Task<IActionResult> Login(LoginRequestDto request)
	{
		var token = await _authService.Login(request);
		return Ok(new { token });
	}

	[HttpPost("loginAuthCode")]
	public async Task<IActionResult> LoginAuthCode(LoginAuthCodeRequestDto request)
	{
		await _authService.LoginAuthCode(request);
		return Ok();
	}

	[HttpPost("verifyAuthCode")]
	public async Task<IActionResult> VerifyAuthCode(VerifyAuthCodeRequestDto request)
	{
		var token = await _authService.VerifyAuthCode(request);
		return Ok(new { token });
	}
}
