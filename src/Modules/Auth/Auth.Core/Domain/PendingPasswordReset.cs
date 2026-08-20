namespace Auth.Core.Domain;

public sealed class PendingPasswordReset
{
	private PendingPasswordReset() { }

	public Guid Id { get; private set; }

	public Guid UserId { get; private set; }

	public string CodeHash { get; private set; } = null!;

	public int AttemptCount { get; private set; }

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset ExpiresAtUtc { get; private set; }

	public static PendingPasswordReset Create(
		Guid id,
		Guid userId,
		string codeHash,
		DateTimeOffset createdAtUtc,
		DateTimeOffset expiresAtUtc) =>
		new()
		{
			Id = id,
			UserId = userId,
			CodeHash = codeHash,
			CreatedAtUtc = createdAtUtc,
			ExpiresAtUtc = expiresAtUtc,
			AttemptCount = 0
		};

	public bool IsExpired(DateTimeOffset utcNow) => ExpiresAtUtc <= utcNow;

	public void RecordFailedAttempt() => AttemptCount++;

	public bool HasExceededAttempts(int maxAttempts) => AttemptCount >= maxAttempts;
}
