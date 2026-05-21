using CloudDrive.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;

namespace CloudDrive.Shared.Api.Extensions;

internal static class ErrorTypeMapper
{
	public static int ToStatusCode(ErrorType type) => type switch
	{
		ErrorType.Validation => StatusCodes.Status400BadRequest,
		ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
		ErrorType.Forbidden => StatusCodes.Status403Forbidden,
		ErrorType.NotFound => StatusCodes.Status404NotFound,
		ErrorType.Conflict => StatusCodes.Status409Conflict,
		_ => StatusCodes.Status500InternalServerError
	};

	public static string ToTitle(ErrorType type) => type switch
	{
		ErrorType.Validation => "Validation failed",
		ErrorType.Unauthorized => "Unauthorized",
		ErrorType.Forbidden => "Forbidden",
		ErrorType.NotFound => "Resource not found",
		ErrorType.Conflict => "Conflict",
		_ => "Server Error"
	};
}