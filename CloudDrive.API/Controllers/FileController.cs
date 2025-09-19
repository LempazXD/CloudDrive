using CloudDrive.Application.Interfaces;
using CloudDrive.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CloudDrive.API.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
	private readonly IFileService _fileService;

	public FileController(IFileService fileManager)
	{
		_fileService = fileManager;
	}

	[HttpPost]
	public async Task<IActionResult> UploadFile(IFormFile file)
	{
		if (file == null || file.Length == 0)
			return BadRequest("Пустой файл"); // !!! ??

		using var stream = new MemoryStream();
		await file.CopyToAsync(stream);

		//await _fileService.UploadFile(GetUserId(), file.FileName, stream.ToArray());

		return Ok("Файл успешно загружен"); // !!! Добавить поддержку неск языков
	}

	[HttpGet]
	public async Task<IActionResult> GetFiles()
	{
		var userId = GetUserId();
		var files = await _fileService.GetUserFiles(userId);
		return Ok(files);
	}

	[HttpGet]
	public async Task<IActionResult> GetFileInfo([FromBody] GetFileInfoRequest fileId)
	{
		return Ok();
	}

	private Guid GetUserId() // !!! Переместить (Сначала самому подумать куда запихнуть)
	{
		var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(userIdClaim))
			throw new Exception("Пользователь не авторизован");

		return Guid.Parse(userIdClaim);
	}
}
