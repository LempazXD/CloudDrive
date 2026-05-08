namespace CloudDrive.Common.Results;

public class Result
{
	public bool IsSuccess { get; }
	public bool IsFailure => !IsSuccess;
	public Error Error { get; }

	protected Result(bool isSuccess, Error error)
	{
		if (isSuccess && error != Error.None)
			throw new ArgumentException("Success result cannot have an error", nameof(error));
		if (!isSuccess && error == Error.None)
			throw new ArgumentException("Failure result must have an error", nameof(error));


		IsSuccess = isSuccess;
		Error = error;
	}

	public static Result Success() => new(true, Error.None);
	public static Result Failure(Error error) => new(false, error);

	public static implicit operator Result(Error error) => Failure(error);
}

public class Result<TValue> : Result
{
	public TValue Value => IsSuccess
		? field!
		: throw new InvalidOperationException("Cannot access Value of a failed result");

	private Result(TValue value)
		: base(true, Error.None)
	{
		Value = value;
	}

	private Result(Error error)
		: base(false, error)
	{
		Value = default;
	}

	public static Result<TValue> Success(TValue value) => new(value);
	public new static Result<TValue> Failure(Error error) => new(error);

	public static implicit operator Result<TValue>(TValue value) => Success(value);
	public static implicit operator Result<TValue>(Error error) => Failure(error);
}