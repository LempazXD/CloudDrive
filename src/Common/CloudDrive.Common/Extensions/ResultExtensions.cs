using CloudDrive.Common.Results;

namespace CloudDrive.Common.Extensions;

public static class ResultExtensions
{
	extension(Result result)
	{
		// Convert Result to Result<T>
		public Result<T> ToValueResult<T>(T value)
		{
			return result.IsSuccess
				? Result<T>.Success(value)
				: Result<T>.Failure(result.Error);
		}
	}

	extension<T>(Result<T> result)
	{
		// Transforms the value inside Result without leaving the railway
		public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
		{
			return result.IsSuccess
				? Result<TOut>.Success(mapper(result.Value))
				: Result<TOut>.Failure(result.Error);
		}

		// Chain operations that return Results or fail
		public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
		{
			return result.IsSuccess
				? binder(result.Value)
				: Result<TOut>.Failure(result.Error);
		}

		// Collapses Result into a value - the exit from the railway
		public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<AppError, TOut> onFailure)
		{
			return result.IsSuccess
				? onSuccess(result.Value)
				: onFailure(result.Error);
		}

		// Execute side action on success (logs, events, etc)
		public Result<T> Tap(Action<T> action)
		{
			if (result.IsSuccess)
				action(result.Value);
			return result;
		}

		// Executa side action on failure (logs, events, etc)
		public Result<T> TapError(Action<AppError> action)
		{
			if (result.IsFailure)
				action(result.Error);
			return result;
		}

		// Validates a condition on the value mid-pipeline
		public Result<T> Ensure(Func<T, bool> predicate, AppError error)
		{
			if (!result.IsSuccess)
				return result;
			return predicate(result.Value) ? result : Result<T>.Failure(error);
		}
	}
}
