using CloudDrive.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CloudDrive.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{

	private readonly IAuthService _authService;

	public AccountController(IAuthService authService)
	{
		_authService = authService;
	}

}
