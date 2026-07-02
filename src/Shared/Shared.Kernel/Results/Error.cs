namespace Shared.Kernel.Results;

public record Error
{
	private Error(
		string code,
		ErrorType type)
	{
		Code = code;
		Type = type;
	}

	/// <summary> Convention: "Module.Entity.Reason" </summary>
	public string Code { get; }

	public ErrorType Type { get; }


	public static Error NotFound(string code) => new(code, ErrorType.NotFound);

	public static Error Validation(string code) => new(code, ErrorType.Validation);

	public static Error Conflict(string code) => new(code, ErrorType.Conflict);

	public static Error Unauthorized(string code) => new(code, ErrorType.Unauthorized);

	public static Error Forbidden(string code) => new(code, ErrorType.Forbidden);
}

public enum ErrorType
{
	Validation = 1,
	NotFound,
	Conflict,
	Unauthorized,
	Forbidden
}