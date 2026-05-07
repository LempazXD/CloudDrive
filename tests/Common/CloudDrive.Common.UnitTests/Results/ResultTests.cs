using CloudDrive.Common.Results;
using FluentAssertions;

namespace CloudDrive.Common.UnitTests.Results;

public class ResultTests
{
	private static readonly Error SomeError = Error.Failure("X.Fail", "fail");

	// ── Result ───────────────────────────────────────────────────────────────

	[Fact]
	public void SuccessShouldBeSuccessWithNoneError()
	{
		var result = Result.Success();

		result.IsSuccess.Should().BeTrue();
		result.IsFailure.Should().BeFalse();
		result.Error.Should().Be(Error.None);
	}

	[Fact]
	public void GenericSuccessShouldHaveNoneError()
	{
		var result = Result<int>.Success(42);
		result.IsSuccess.Should().BeTrue();
		result.IsFailure.Should().BeFalse();
		result.Error.Should().Be(Error.None);
	}

	[Fact]
	public void FailureShouldBeFailureWithGivenError()
	{
		var result = Result.Failure(SomeError);

		result.IsSuccess.Should().BeFalse();
		result.IsFailure.Should().BeTrue();
		result.Error.Should().Be(SomeError);
	}

	[Fact]
	public void ConstructorSuccessWithNonNoneErrorShouldThrow()
	{
		var act = () => new TestResult(true, SomeError);
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ConstructorFailureWithNoneErrorShouldThrow()
	{
		var act = () => new TestResult(false, Error.None);
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ImplicitOperatorFromAppErrorShouldCreateFailure()
	{
		Result result = SomeError;

		result.IsFailure.Should().BeTrue();
		result.Error.Should().Be(SomeError);
	}

	// ── Result<T> ────────────────────────────────────────────────────────────

	[Fact]
	public void GenericSuccessShouldContainValue()
	{
		var result = Result<int>.Success(42);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().Be(42);
	}

	[Fact]
	public void GenericSuccessWithNullValueShouldStillBeSuccess()
	{
		var result = Result<string?>.Success(null);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().BeNull();
	}

	[Fact]
	public void GenericFailureAccessingValueShouldThrowInvalidOperation()
	{
		var result = Result<int>.Failure(SomeError);

		var act = () => result.Value;
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void GenericFailureShouldHaveError()
	{
		var result = Result<int>.Failure(SomeError);

		result.IsFailure.Should().BeTrue();
		result.Error.Should().Be(SomeError);
	}

	[Fact]
	public void GenericImplicitOperatorFromValueShouldCreateSuccess()
	{
		Result<string> result = "hello";

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().Be("hello");
	}

	[Fact]
	public void GenericImplicitOperatorFromAppErrorShouldCreateFailure()
	{
		Result<string> result = SomeError;

		result.IsFailure.Should().BeTrue();
		result.Error.Should().Be(SomeError);
	}

	// Helper: открывает доступ к защищённому конструктору Result
	private sealed class TestResult(bool isSuccess, Error error) : Result(isSuccess, error);
}