using System.Diagnostics;
using Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;

namespace Shared.Api.Extensions;

internal static class ErrorTypeMapper
{
	public static (int StatusCode, string TitleCode) Map(ErrorType type) => type switch
	{
		ErrorType.Validation => (StatusCodes.Status400BadRequest, "Http.Title.Validation"),
		ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Http.Title.Unauthorized"),
		ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Http.Title.Forbidden"),
		ErrorType.NotFound => (StatusCodes.Status404NotFound, "Http.Title.NotFound"),
		ErrorType.Conflict => (StatusCodes.Status409Conflict, "Http.Title.Conflict"),
		ErrorType.LockedOut => (StatusCodes.Status423Locked, "Http.Title.LockedOut"),
		_ => throw new UnreachableException($"Unmapped {nameof(ErrorType)}: {type}")
	};
}
