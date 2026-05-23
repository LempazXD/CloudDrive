using CloudDrive.Shared.Api.ExceptionHandling;
using CloudDrive.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CloudDrive.Shared.Api.Extensions;

public static class ResultExtensions
{
	extension(Result result)
	{
		private ProblemHttpResult ToProblemDetails()
		{
			if (result.IsSuccess)
				throw new InvalidOperationException("Cannot convert successful result to ProblemDetails.");

			return TypedResults.Problem(
				statusCode: ErrorTypeMapper.ToStatusCode(result.Error!.Type),
				title: ErrorTypeMapper.ToTitle(result.Error.Type),
				extensions: new Dictionary<string, object?>
				{
					[ProblemDetailsLocalizer.ErrorCodeKey] = result.Error.Code
				});
		}

		public Results<TSuccess, ProblemHttpResult> Match<TSuccess>(Func<TSuccess> onSuccess)
			where TSuccess : IResult
		{
			return result.IsSuccess
				? onSuccess()
				: result.ToProblemDetails();
		}
	}

	extension<T>(Result<T> result)
	{
		public Results<TSuccess, ProblemHttpResult> Match<TSuccess>(Func<T, TSuccess> onSuccess)
			where TSuccess : IResult
		{
			return result.IsSuccess
				? onSuccess(result.Value)
				: result.ToProblemDetails();
		}
	}
}