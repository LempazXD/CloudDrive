using CloudDrive.Application.Interfaces;
using CloudDrive.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudDrive.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrationController : ControllerBase
{

	private readonly IAuthService _authService;

	public RegistrationController(IAuthService authService)
	{
		_authService = authService;
	}

	[AllowAnonymous]
	[HttpPost]
	public async Task<IActionResult> SendRegisterCode(SendRegisterCodeRequest request) // !!! Register или Registration
	{
		return Ok();
	}

	[AllowAnonymous]
	[HttpPost]
	public async Task<IActionResult> Register(RegisterRequest request)
	{
		return Ok();
	}
}
