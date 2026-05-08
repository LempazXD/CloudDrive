namespace CloudDrive.Common.Results;

public sealed record Error
{
	private Error(
		string code,
		string description,
		ErrorType type)
	{
		if (type != ErrorType.None)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(code);
			ArgumentException.ThrowIfNullOrWhiteSpace(description);
		}

		Code = code;
		Description = description;
		Type = type;
	}

	/// <summary> Convention: "Module.Entity.Reason" </summary>
	public string Code { get; }
	/// <summary> Описание ошибки для пользователя </summary>
	public string Description { get; }
	public ErrorType Type { get; }


	public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

	public static Error Failure(string code, string description) =>
		new(code, description, ErrorType.Failure);

	public static Error NotFound(string code, string description) =>
		new(code, description, ErrorType.NotFound);

	public static Error Validation(string code, string description) =>
		new(code, description, ErrorType.Validation);

	public static Error Conflict(string code, string description) =>
		new(code, description, ErrorType.Conflict);

	public static Error Unauthorized(string code, string description) =>
		new(code, description, ErrorType.Unauthorized);

	public static Error Forbidden(string code, string description) =>
		new(code, description, ErrorType.Forbidden);

	public static Error Unexpected(string description = "An unexpected error occurred") =>
		new("General.Unexpected", description, ErrorType.Unexpected);
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