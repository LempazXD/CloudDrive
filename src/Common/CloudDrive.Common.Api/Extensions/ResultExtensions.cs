using CloudDrive.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CloudDrive.Common.Api.Extensions;

public static class ResultExtensions
{
	extension(Result result)
	{
		public IResult ToApiResult()
		{
			return result.IsSuccess
				? TypedResults.NoContent()
				: Failure(result.Error);
		}
	}

	extension<T>(Result<T> result)
	{
		public IResult ToApiResult()
		{
			return result.IsSuccess
				? TypedResults.Ok(result.Value)
				: Failure(result.Error);
		}

		public IResult ToCreatedResult(string uri)
		{
			return result.IsSuccess
				? TypedResults.Created(uri, result.Value)
				: Failure(result.Error);
		}
	}

	private static ProblemHttpResult Failure(Error error) =>
		TypedResults.Problem(error.ToProblemDetails());
}
