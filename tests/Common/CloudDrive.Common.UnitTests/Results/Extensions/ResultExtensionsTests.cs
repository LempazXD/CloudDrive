using CloudDrive.Common.Extensions;
using CloudDrive.Common.Results;
using FluentAssertions;

namespace CloudDrive.Common.UnitTests.Results.Extensions;

public class ResultExtensionsTests
{

	private readonly Error _testError = Error.Failure("Test.Error", "Something went wrong");

	[Fact]
	public void MapShouldTransformValueWhenResultIsSuccess()
	{
		var result = Result<int>.Success(5);
		var finalResult = result.Map(x => x * 2);

		finalResult.IsSuccess.Should().BeTrue();
		finalResult.Value.Should().Be(10);
	}

	[Fact]
	public void MapShouldReturnFailureWhenResultIsFailure()
	{
		var result = Result<int>.Failure(_testError);
		var finalResult = result.Map(x => x * 2);

		finalResult.IsFailure.Should().BeTrue();
		finalResult.Error.Should().Be(_testError);
	}

	[Fact]
	public void EnsureShouldReturnFailureWhenPredicateFails()
	{
		var result = Result<int>.Success(10);
		var validationError = Error.Validation("Value.TooSmall", "Value must be > 100");
		var finalResult = result.Ensure(x => x > 100, validationError);

		finalResult.IsFailure.Should().BeTrue();
		finalResult.Error.Should().Be(validationError);
	}

	[Fact]
	public async Task BindAsyncShouldChainCallsWhenSuccess()
	{
		var result = Result<string>.Success("input");
		Func<string, Task<Result<int>>> nextStep =
			s => Task.FromResult(Result<int>.Success(s.Length));
		var finalResult = await result.Bind(nextStep);

		finalResult.IsSuccess.Should().BeTrue();
		finalResult.Value.Should().Be(5);
	}

	[Fact]
	public void TapShouldExecuteActionOnlyOnSuccess()
	{
		var wasCalled = false;
		var result = Result<int>.Failure(_testError);
		result.Tap(_ => wasCalled = true);

		wasCalled.Should().BeFalse();
		result.IsFailure.Should().BeTrue();
	}

	[Fact]
	public void MatchShouldReturnSuccessPathWhenResultIsSuccess()
	{
		var result = Result<string>.Success("ok");
		var output = result.Match(
			onSuccess: val => "Success: " + val,
			onFailure: err => "Error: " + err.Code
		);

		output.Should().Be("Success: ok");
	}

	[Fact]
	public async Task TapTaskExtensionShouldAwaitAndExecuteWhenSuccess()
	{
		var task = Task.FromResult(Result<int>.Success(1));
		var sideEffectDone = false;

		await task.Tap(() =>
		{
			sideEffectDone = true;
			return Task.CompletedTask;
		});

		sideEffectDone.Should().BeTrue();
	}
}
