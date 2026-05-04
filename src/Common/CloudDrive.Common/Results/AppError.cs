namespace CloudDrive.Common.Results;

public sealed record AppError
{
	private AppError(
		string code,
		string description,
		ErrorType type)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(code);
		ArgumentException.ThrowIfNullOrWhiteSpace(description);

		Code = code;
		Description = description;
		Type = type;
	}

	/// <summary> Convention: "Module.Entity.Reason" </summary>
	public string Code { get; }
	/// <summary> User-friendly error description </summary>
	public string Description { get; }
	public ErrorType Type { get; }


	public static readonly AppError None = new(string.Empty, string.Empty, ErrorType.None);

	public static AppError Failure(string code, string description) =>
		new(code, description, ErrorType.Failure);

	public static AppError NotFound(string code, string description) =>
		new(code, description, ErrorType.NotFound);

	public static AppError Validation(string code, string description) =>
		new(code, description, ErrorType.Validation);

	public static AppError Conflict(string code, string description) =>
		new(code, description, ErrorType.Conflict);

	public static AppError Unauthorized(string code, string description) =>
		new(code, description, ErrorType.Unauthorized);

	public static AppError Forbidden(string code, string description) =>
		new(code, description, ErrorType.Forbidden);

	public static AppError Unexpected(string description = "An unexpected error occurred") =>
		new AppError("Genaral.Unexpected", description, ErrorType.Unexpected);
}

public enum ErrorType
{
	None = -1,
	Unexpected = 0,
	Failure = 1,
	NotFound = 2,
	Validation = 3,
	Conflict = 4,
	Unauthorized = 5,
	Forbidden = 6
}