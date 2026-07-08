using Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Shared.Api.ExceptionHandling;

namespace Shared.Api.Extensions;

public static class ResultExtensions
{
	extension(Result result)
	{
		private ProblemHttpResult ToProblemDetails()
		{
			if (result.IsSuccess)
				throw new InvalidOperationException("Cannot convert successful result to ProblemDetails.");

			var (statusCode, titleCode) = ErrorTypeMapper.Map(result.Error!.Type);

			var extensions = new Dictionary<string, object?>
			{
				[ProblemDetailsLocalizer.ErrorCodeKey] = result.Error.Code,
				[ProblemDetailsLocalizer.TitleCodeKey] = titleCode
			};

			if (result.Error is ValidationError validationError)
			{
				extensions[ProblemDetailsLocalizer.ValidationFailuresKey] = validationError.Failures
					.GroupBy(failure => failure.PropertyName)
					.ToDictionary(group => group.Key, group => group.Select(failure => failure.ReasonCode).ToArray());
			}

			if (result.Error is LockedOutError lockedOutError)
				extensions[RetryAfterEnricher.RetryAfterUtcKey] = lockedOutError.RetryAfterUtc;

			return TypedResults.Problem(statusCode: statusCode, extensions: extensions);
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
