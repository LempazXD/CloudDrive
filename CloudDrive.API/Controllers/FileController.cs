using CloudDrive.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CloudDrive.API.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
	private readonly IFileManager _fileManager;

	public FileController(IFileManager fileManager)
	{
		_fileManager = fileManager;
	}

	[HttpGet]
	public async Task<IActionResult> GetFiles()
	{
		var userId = GetUserId();
		var files = await _fileManager.GetFiles(userId); // показывает ВООБЩЕ ВСЕ файлы
		return Ok(files);
	}

	private int GetUserId()
	{
		var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(userIdClaim))
			throw new Exception("Пользователь не авторизован");

		return int.Parse(userIdClaim);
	}
}
