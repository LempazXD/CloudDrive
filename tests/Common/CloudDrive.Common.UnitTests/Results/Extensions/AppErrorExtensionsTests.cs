using CloudDrive.Common.Api.Extensions;
using CloudDrive.Common.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace CloudDrive.Common.UnitTests.Results.Extensions;

public class AppErrorExtensionsTests
{
	// ── Status + Title mapping ────────────────────────────────────────────────

	[Fact]
	public void ToProblemDetailsNotFoundShouldMapCorrectly()
	{
		var error = Error.NotFound("Files.File.NotFound", "File not found");
		var problem = error.ToProblemDetails();

		problem.Status.Should().Be(StatusCodes.Status404NotFound);
		problem.Title.Should().Be("Not Found");
		problem.Detail.Should().Be("File not found");
		problem.Extensions["code"].Should().Be("Files.File.NotFound");
	}

	[Fact]
	public void ToProblemDetailsValidationShouldMapCorrectly()
	{
		var error = Error.Validation("Files.Upload.TooLarge", "Max size is 100MB");
		var problem = error.ToProblemDetails();

		problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
		problem.Title.Should().Be("Validation Error");
		problem.Detail.Should().Be("Max size is 100MB");
	}

	public static TheoryData<Error, int> StatusMappingCases => new()
	{
		{ Error.Conflict("Files.File.AlreadyExists", "File already exists"),  StatusCodes.Status409Conflict },
		{ Error.Unauthorized("Auth.Token.Expired", "Token expired"),          StatusCodes.Status401Unauthorized },
		{ Error.Forbidden("Files.File.NoAccess", "Access denied"),            StatusCodes.Status403Forbidden }
	};

	[Theory]
	[MemberData(nameof(StatusMappingCases))]
	public void ToProblemDetailsShouldMapStatusCorrectly(Error error, int expectedStatus)
	{
		error.ToProblemDetails().Status.Should().Be(expectedStatus);
	}

	// ── 500 leakage prevention ────────────────────────────────────────────────

	[Fact]
	public void ToProblemDetailsUnexpectedShouldHideInternalDetails()
	{
		var error = Error.Unexpected("Connection string: Server=prod-db;Password=secret");
		var problem = error.ToProblemDetails();

		problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
		problem.Detail.Should().Be("An unexpected error occurred");
	}

	[Fact]
	public void ToProblemDetailsFailureShouldExposeDescription()
	{
		var error = Error.Failure("Auth.Login.Invalid", "Invalid credentials");
		var problem = error.ToProblemDetails();

		problem.Detail.Should().Be("Invalid credentials");
	}

	// ── Code extension field ─────────────────────────────────────────────────

	[Fact]
	public void ToProblemDetailsUnexpectedShouldStillIncludeCode()
	{
		var error = Error.Unexpected();
		var problem = error.ToProblemDetails();

		problem.Extensions.Should().ContainKey("code");
		problem.Extensions["code"].Should().Be("General.Unexpected");
	}
}
