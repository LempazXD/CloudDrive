using CloudDrive.Shared.Kernel.Results;

namespace CloudDrive.Shared.Kernel.Extensions;

public static class ResultExtensions
{
	extension(Result result)
	{
		// Прикрепляет значение к не-generic Result, превращая его в Result<T>.
		public Result<T> ToValueResult<T>(T value)
		{
			return result.IsSuccess
				? Result.Success<T>(value)
				: Result.Failure<T>(result.Error!);
		}
	}

	extension<T>(Result<T> result)
	{
		// Проверяет бизнес-условие на значении внутри успешного Result.
		public Result<T> Ensure(
			Func<T, bool> predicate,
			Error error)
		{
			if (result.IsFailure)
				return result;

			return predicate(result.Value)
				? result
				: Result.Failure<T>(error);
		}

		// Трансформирует значение внутри успешного Result из T в TOut.
		// Маппинг сущностей в DTO, проекция свойств, конвертация типов.
		public Result<TOut> Map<TOut>(
			Func<T, TOut> mappingFunc)
		{
			return result.IsSuccess
				? Result.Success<TOut>(mappingFunc(result.Value))
				: Result.Failure<TOut>(result.Error!);
		}

		// Трансформирует ошибку, не затрагивая значение успешного результата.
		public Result<T> MapError(Func<Error, Error> errorMapper)
		{
			return result.IsFailure
				? Result.Failure<T>(errorMapper(result.Error!))
				: result;
		}

		// Связывает зависимую операцию, которая сама может завершиться ошибкой.
		// Используется вместо Map, когда следующий шаг возвращает Result (репозиторий, сервис).
		public Result<TOut> Bind<TOut>(
			Func<T, Result<TOut>> func)
		{
			return result.IsFailure
				? Result.Failure<TOut>(result.Error!)
				: func(result.Value);
		}

		public async Task<Result<TOut>> Bind<TOut>(
			Func<T, Task<Result<TOut>>> func)
		{
			return result.IsFailure
				? Result.Failure<TOut>(result.Error!)
				: await func(result.Value);
		}

		// Выполняет побочное действие на успешном пути, не меняя результат.
		// Логирование, публикация событий, отправка уведомлений.
		public Result<T> Tap(Action<T> action)
		{
			if (result.IsSuccess)
				action(result.Value);
			return result;
		}

		public async Task<Result<T>> Tap(Func<T, Task> func)
		{
			if (result.IsSuccess)
				await func(result.Value);
			return result;
		}

		// Выполняет побочное действие на пути ошибки, не меняя результат.
		// Логирования ошибок, записи метрик.
		public Result<T> TapError(Action<Error> action)
		{
			if (result.IsFailure)
				action(result.Error!);
			return result;
		}

		// Терминальная операция: сворачивает Result в конечное значение, обрабатывая оба пути.
		// Используется на границе пайплайна для преобразования Result в HTTP-ответ или DTO.
		public TOut Match<TOut>(
			Func<T, TOut> onSuccess,
			Func<Error, TOut> onFailure)
		{
			return result.IsSuccess
				? onSuccess(result.Value)
				: onFailure(result.Error!);
		}
	}

	extension<T>(Task<Result<T>> resultTask)
	{
		public async Task<Result<TOut>> Bind<TOut>(
			Func<T, Task<Result<TOut>>> func)
		{
			var result = await resultTask;

			return result.IsFailure
				? Result.Failure<TOut>(result.Error!)
				: await func(result.Value);
		}

		public async Task<Result<TOut>> Map<TOut>(
			Func<T, TOut> mappingFunc)
		{
			var result = await resultTask;

			return result.IsSuccess
				? Result.Success<TOut>(mappingFunc(result.Value))
				: Result.Failure<TOut>(result.Error!);
		}

		public async Task<Result<T>> Ensure(
			Func<T, bool> predicate,
			Error error)
		{
			var result = await resultTask;

			if (result.IsFailure)
				return result;

			return predicate(result.Value)
				? result
				: Result.Failure<T>(error);
		}

		public async Task<Result<T>> Tap(Func<Task> func)
		{
			var result = await resultTask;

			if (result.IsSuccess)
				await func();
			return result;
		}

		public async Task<TOut> Match<TOut>(
			Func<T, TOut> onSuccess,
			Func<Error, TOut> onFailure)
		{
			var result = await resultTask;

			return result.IsSuccess
				? onSuccess(result.Value)
				: onFailure(result.Error!);
		}
	}
}