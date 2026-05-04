using CloudDrive.Common.Results;
using Microsoft.AspNetCore.Http;

namespace CloudDrive.Common.Api.Extensions;

public static class ErrorTypeExtensions
{
	extension(ErrorType type)
	{
		public int ToHttpStatusCode() => type switch
		{
			ErrorType.Failure => StatusCodes.Status400BadRequest,
			ErrorType.NotFound => StatusCodes.Status404NotFound,
			ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
			ErrorType.Conflict => StatusCodes.Status409Conflict,
			ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
			ErrorType.Forbidden => StatusCodes.Status403Forbidden,
			ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
			_ => StatusCodes.Status500InternalServerError
		};

		public string ToTitle() => type switch
		{
			ErrorType.Failure => "Failure",
			ErrorType.NotFound => "Not Found",
			ErrorType.Validation => "Validation Error",
			ErrorType.Conflict => "Conflict",
			ErrorType.Unauthorized => "Unauthorized",
			ErrorType.Forbidden => "Forbidden",
			ErrorType.Unexpected => "An unexpected error occured",
			_ => "Internal Server Error"
		};
	}
}
