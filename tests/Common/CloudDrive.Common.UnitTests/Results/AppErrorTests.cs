using CloudDrive.Common.Results;
using FluentAssertions;

namespace CloudDrive.Common.UnitTests.Results;

public class AppErrorTests
{
	// ── None ────────────────────────────────────────────────────────────────

	[Fact]
	public void NoneShouldHaveEmptyCodeDescriptionAndNoneType()
	{
		Error.None.Code.Should().BeEmpty();
		Error.None.Description.Should().BeEmpty();
		Error.None.Type.Should().Be(ErrorType.None);
	}

	// ── Validation ───────────────────────────────────────────────────────────

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void FactoryEmptyOrWhitespaceCodeThrows(string code)
	{
		var act = () => Error.Failure(code, "description");
		act.Should().Throw<ArgumentException>();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void FactoryEmptyOrWhitespaceDescriptionThrows(string description)
	{
		var act = () => Error.Failure("Module.Entity.Reason", description);
		act.Should().Throw<ArgumentException>();
	}

	// ── Factory methods ──────────────────────────────────────────────────────

	[Fact]
	public void FailureShouldCreateErrorWithCorrectFields()
	{
		var error = Error.Failure("Auth.Login.Invalid", "Invalid credentials");

		error.Type.Should().Be(ErrorType.Failure);
		error.Code.Should().Be("Auth.Login.Invalid");
		error.Description.Should().Be("Invalid credentials");
	}

	public static TheoryData<Func<Error>, ErrorType> FactoryTypeCases => new()
	{
		{ () => Error.NotFound("X.NotFound", "msg"),          ErrorType.NotFound     },
		{ () => Error.Validation("X.Validation", "msg"),      ErrorType.Validation   },
		{ () => Error.Conflict("X.Conflict", "msg"),          ErrorType.Conflict     },
		{ () => Error.Unauthorized("X.Unauthorized", "msg"),  ErrorType.Unauthorized },
		{ () => Error.Forbidden("X.Forbidden", "msg"),        ErrorType.Forbidden    },
	};

	[Theory]
	[MemberData(nameof(FactoryTypeCases))]
	public void FactoryShouldCreateErrorWithMatchingType(Func<Error> factory, ErrorType expectedType)
	{
		factory().Type.Should().Be(expectedType);
	}

	// ── Unexpected ───────────────────────────────────────────────────────────

	[Fact]
	public void UnexpectedDefaultDescriptionShouldUseDefault()
	{
		var error = Error.Unexpected();

		error.Type.Should().Be(ErrorType.Unexpected);
		error.Code.Should().Be("General.Unexpected");
		error.Description.Should().Be("An unexpected error occurred");
	}

	[Fact]
	public void UnexpectedCustomDescriptionShouldUseProvided()
	{
		var error = Error.Unexpected("Database connection lost");
		error.Description.Should().Be("Database connection lost");
	}

	// ── Record equality ──────────────────────────────────────────────────────

	[Fact]
	public void TwoErrorsWithDifferentFieldsShouldNotBeEqual()
	{
		var a = Error.Failure("X.Fail", "fail");
		var b = Error.NotFound("X.Fail", "fail");
		a.Should().NotBe(b);
	}
}