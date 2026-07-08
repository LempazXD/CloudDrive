namespace Shared.Kernel.Results;

public sealed record LockedOutError : Error
{
	public DateTimeOffset RetryAfterUtc { get; }

	internal LockedOutError(string code, DateTimeOffset retryAfterUtc)
		: base(code, ErrorType.LockedOut)
	{
		RetryAfterUtc = retryAfterUtc;
	}

}
