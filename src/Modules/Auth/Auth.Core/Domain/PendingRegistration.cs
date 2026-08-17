namespace Auth.Core.Domain;

public sealed class PendingRegistration
{
	private PendingRegistration() { }

	public Guid Id { get; private set; }

	public string NormalizedEmail { get; private set; } = null!;

	public string Email { get; private set; } = null!;

	public string Username { get; private set; } = null!;

	public string PasswordHash { get; private set; } = null!;

	public string CodeHash { get; private set; } = null!;

	public int AttemptCount { get; private set; }

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset ExpiresAtUtc { get; private set; }

	public static PendingRegistration Create(
		Guid id,
		string normalizedEmail,
		string email,
		string username,
		string passwordHash,
		string codeHash,
		DateTimeOffset createdAtUtc,
		DateTimeOffset expiresAtUtc) =>
		new()
		{
			Id = id,
			NormalizedEmail = normalizedEmail,
			Email = email,
			Username = username,
			PasswordHash = passwordHash,
			CodeHash = codeHash,
			CreatedAtUtc = createdAtUtc,
			ExpiresAtUtc = expiresAtUtc,
			AttemptCount = 0
		};

	public bool IsExpired(DateTimeOffset utcNow) => ExpiresAtUtc <= utcNow;

	public void RecordFailedAttempt() => AttemptCount++;

	public bool HasExceededAttempts(int maxAttempts) => AttemptCount >= maxAttempts;
}
