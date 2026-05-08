using CloudDrive.Common.Results;

namespace CloudDrive.Common.Extensions;

public static class ResultExtensions
{
	extension(Result result)
	{
		public Result<T> ToValueResult<T>(T value)
		{
			return result.IsSuccess
				? Result<T>.Success(value)
				: Result<T>.Failure(result.Error);
		}
	}

	extension<T>(Result<T> result)
	{
		// Проверяет условие на значении mid-pipeline
		public Result<T> Ensure(
			Func<T, bool> predicate,
			Error error)
		{
			if (result.IsFailure)
				return result;

			return predicate(result.Value)
				? result
				: Result<T>.Failure(error);
		}

		// Трансформирует значение внутри Result в случае успеха
		public Result<TOut> Map<TOut>(
			Func<T, TOut> mappingFunc)
		{
			return result.IsSuccess
				? Result<TOut>.Success(mappingFunc(result.Value))
				: Result<TOut>.Failure(result.Error);
		}

		// Связывает операции, которые возвращают Result или завершаются ошибкой
		public Result<TOut> Bind<TOut>(
			Func<T, Result<TOut>> func)
		{
			return result.IsFailure
				? Result<TOut>.Failure(result.Error)
					: func(result.Value);
		}

		public async Task<Result<TOut>> Bind<TOut>(
			Func<T, Task<Result<TOut>>> func)
		{
			return result.IsFailure
				? Result<TOut>.Failure(result.Error)
				:  await func(result.Value);
		}

		// Выполняет побочное действие в случае успеха (логи, события и т.д.)
		public Result<T> Tap(Action<T> action)
		{
			if (result.IsSuccess)
				action(result.Value);
			return result;
		}

		public async Task<Result<T>> Tap(Func<Task> func)
		{
			if (result.IsSuccess)
				await func();
			return result;
		}

		// Преобразует Result в конечное значение, обрабатывая оба случая: успех и ошибку
		public TOut Match<TOut>(
			Func<T, TOut> onSuccess,
			Func<Error, TOut> onFailure)
		{
			return result.IsSuccess
				? onSuccess(result.Value)
				: onFailure(result.Error);
		}
	}

	extension<T>(Task<Result<T>> resultTask)
	{
		public async Task<Result<T>> Tap(Func<Task> func)
		{
			var result = await resultTask;

			if (result.IsSuccess)
				await func();
			return result;
		}
	}
}
