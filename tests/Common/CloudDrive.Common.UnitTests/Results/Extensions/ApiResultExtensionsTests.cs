using CloudDrive.Common.Api.Extensions;
using CloudDrive.Common.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CloudDrive.Common.UnitTests.Results.Extensions;

public class ApiResultExtensionsTests
{
	private static readonly Error Error = Error.Failure("X.Fail", "fail");

	// ── Result (non-generic) ─────────────────────────────────────────────────

	[Fact]
	public void ResultToApiResultOnSuccessShouldReturn204NoContent()
	{
		var result = Result.Success();
		var apiResult = result.ToApiResult();

		apiResult.Should().BeOfType<NoContent>();
	}

	[Fact]
	public void ResultToApiResultOnFailureShouldReturnProblem()
	{
		var result = Result.Failure(Error);
		var apiResult = result.ToApiResult();

		apiResult.Should().BeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)apiResult;
		problem.ProblemDetails.Status.Should().Be(400);
		problem.ProblemDetails.Extensions["code"].Should().Be("X.Fail");
	}

	// ── Result<T> ────────────────────────────────────────────────────────────

	[Fact]
	public void ResultTToApiResultOnSuccessShouldReturn200Ok()
	{
		Result<string> result = "hello";
		var apiResult = result.ToApiResult();

		apiResult.Should().BeOfType<Ok<string>>();
		((Ok<string>)apiResult).Value.Should().Be("hello");
	}

	[Fact]
	public void ResultTToApiResultOnFailureShouldReturnProblemWithCorrectStatus()
	{
		Result<string> result = Error.NotFound("X.NotFound", "not found");
		var apiResult = result.ToApiResult();

		apiResult.Should().BeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)apiResult;
		problem.ProblemDetails.Status.Should().Be(404);
		problem.ProblemDetails.Extensions["code"].Should().Be("X.NotFound");
	}

	// ── ToCreatedResult ──────────────────────────────────────────────────────

	[Fact]
	public void ToCreatedResultOnSuccessShouldReturn201WithLocationAndValue()
	{
		Result<int> result = 99;
		var apiResult = result.ToCreatedResult("/api/files/99");

		apiResult.Should().BeOfType<Created<int>>();
		var created = (Created<int>)apiResult;
		created.Value.Should().Be(99);
		created.Location.Should().Be("/api/files/99");
	}

	[Fact]
	public void ToCreatedResultOnFailureShouldReturnProblem()
	{
		Result<int> result = Error.Conflict("X.Conflict", "already exists");
		var apiResult = result.ToCreatedResult("/api/files/99");

		apiResult.Should().BeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)apiResult;
		problem.ProblemDetails.Status.Should().Be(409);
	}

	// ── 500 leakage via API ───────────────────────────────────────────────────

	[Fact]
	public void ToApiResultOnUnexpectedErrorShouldReturn500WithSafeDetail()
	{
		Result<string> result = Error.Unexpected("internal db error details");
		var apiResult = result.ToApiResult();

		var problem = (ProblemHttpResult)apiResult;
		problem.ProblemDetails.Status.Should().Be(500);
		problem.ProblemDetails.Detail.Should().Be("An unexpected error occurred");
		problem.ProblemDetails.Extensions["code"].Should().Be("General.Unexpected");
	}
}
