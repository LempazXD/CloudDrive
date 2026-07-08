namespace Shared.Kernel.Results;

public record Error
{
	private protected Error(
		string code,
		ErrorType type)
	{
		Code = code;
		Type = type;
	}

	/// <summary> Соглашение: "Module.Entity.Reason" </summary>
	public string Code { get; }

	public ErrorType Type { get; }


	public static Error NotFound(string code) => new(code, ErrorType.NotFound);

	public static Error Validation(string code) => new(code, ErrorType.Validation);

	/// <summary> Ошибка валидации с перечнем нарушений по отдельным полям. </summary>
	public static ValidationError Validation(string code, IReadOnlyList<ValidationFailure> failures) =>
		new(code, failures);

	public static Error Conflict(string code) => new(code, ErrorType.Conflict);

	public static Error Unauthorized(string code) => new(code, ErrorType.Unauthorized);

	public static Error Forbidden(string code) => new(code, ErrorType.Forbidden);

	public static Error LockedOut(string code) => new(code, ErrorType.LockedOut);

	/// <summary> Блокировка с известным моментом снятия. </summary>
	public static LockedOutError LockedOut(string code, DateTimeOffset retryAfterUtc) =>
		new(code, retryAfterUtc);
}

public enum ErrorType
{
	Validation = 1,
	NotFound,
	Conflict,
	Unauthorized,
	Forbidden,
	LockedOut
}
