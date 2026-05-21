namespace CloudDrive.Shared.Kernel.Results;

public class Result
{
	public bool IsSuccess { get; }
	public bool IsFailure => !IsSuccess;
	public Error? Error { get; }

	internal Result(bool isSuccess, Error? error)
	{
		if (isSuccess && error is not null)
			throw new ArgumentException("Success result cannot have an error", nameof(error));
		if (!isSuccess && error is null)
			throw new ArgumentException("Failure result must have an error", nameof(error));

		IsSuccess = isSuccess;
		Error = error;
	}

	public static Result Success() => new(true, null);
	public static Result<T> Success<T>(T value) => new(value, true, null);
	public static Result Failure(Error error) => new(false, error);
	public static Result<T> Failure<T>(Error error) => new(default!, false, error);
}

public sealed class Result<T> : Result
{
	public T Value
	{
		get => IsSuccess
			? field
			: throw new InvalidOperationException("Cannot access Value of a failed result");
		private init;
	}

	internal Result(T value, bool isSuccess, Error? error)
		: base(isSuccess, error) => Value = value;

	public static implicit operator Result<T>(Error error) => Failure<T>(error);
}