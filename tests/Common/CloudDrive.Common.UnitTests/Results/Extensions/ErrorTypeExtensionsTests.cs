using CloudDrive.Common.Api.Extensions;
using CloudDrive.Common.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace CloudDrive.Common.UnitTests.Results.Extensions;

public class ErrorTypeExtensionsTests
{
	// ── ToHttpStatusCode ─────────────────────────────────────────────────────

	[Theory]
	[InlineData(ErrorType.Failure, StatusCodes.Status400BadRequest)]
	[InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
	[InlineData(ErrorType.Validation, StatusCodes.Status422UnprocessableEntity)]
	[InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
	[InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
	[InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
	[InlineData(ErrorType.Unexpected, StatusCodes.Status500InternalServerError)]
	public void ToHttpStatusCodeShouldMapToExpectedCode(ErrorType type, int expected)
	{
		type.ToHttpStatusCode().Should().Be(expected);
	}

	// ── ToTitle ──────────────────────────────────────────────────────────────

	[Theory]
	[InlineData(ErrorType.Failure, "Failure")]
	[InlineData(ErrorType.NotFound, "Not Found")]
	[InlineData(ErrorType.Validation, "Validation Error")]
	[InlineData(ErrorType.Conflict, "Conflict")]
	[InlineData(ErrorType.Unauthorized, "Unauthorized")]
	[InlineData(ErrorType.Forbidden, "Forbidden")]
	[InlineData(ErrorType.Unexpected, "An unexpected error occured")]
	public void ToTitleShouldMapToExpectedString(ErrorType type, string expected)
	{
		type.ToTitle().Should().Be(expected);
	}
}
