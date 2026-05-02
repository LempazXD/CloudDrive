namespace CloudDrive.Common.Results;

public class Result
{
	public bool IsSuccess { get; }
	public bool IsFailure => !IsSuccess;
	public AppError Error { get; }

	protected Result(bool isSuccess, AppError error)
	{
		if (isSuccess && error != AppError.None)
			throw new ArgumentException("Success result cannot have an error", nameof(error));
		if (!isSuccess && error == AppError.None)
			throw new ArgumentException("Failure result must have an error", nameof(error));


		IsSuccess = isSuccess;
		Error = error;
	}

	public static Result Success() => new(true, AppError.None);
	public static Result Failure(AppError error) => new(false, error);


	public static implicit operator Result(AppError error) => Failure(error);
}

public class Result<TValue> : Result
{
	private readonly TValue? _value;
	public TValue Value => IsSuccess
		? _value!
		: throw new InvalidOperationException("Cannot access Value of a failed result");

	internal Result(TValue value)
		: base(true, AppError.None)
	{
		_value = value;
	}

	internal Result(AppError error)
		: base(false, error)
	{
		_value = default;
	}

	public static Result<TValue> Success(TValue value) => new(value);
	public new static Result<TValue> Failure(AppError error) => new(error);

	public static implicit operator Result<TValue>(TValue value) => Success(value);
	public static implicit operator Result<TValue>(AppError error) => Failure(error);
}