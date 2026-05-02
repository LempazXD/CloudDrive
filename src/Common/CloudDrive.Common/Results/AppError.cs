namespace CloudDrive.Common.Results;

public sealed record AppError
{
	private AppError(
		string code,
		string description)
	{
		Code = code;
		Description = description;
	}

	public string Code { get; }
	public string Description { get; }


	public static readonly AppError None = new(string.Empty, string.Empty);

	public static AppError Failure(string code, string description) =>
		new(code, description);

	public static AppError NotFound(string code, string description) =>
		new(code, description);

	public static AppError Validation(string code, string description) =>
		new(code, description);

	public static AppError Conflict(string code, string description) =>
		new(code, description);

	public static AppError UnAuthorized(string code, string description) =>
		new(code, description);

	public static AppError Forbidden(string code, string description) =>
		new(code, description);
}