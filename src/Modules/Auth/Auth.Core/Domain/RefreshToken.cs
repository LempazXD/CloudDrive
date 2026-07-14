namespace Auth.Core.Domain;

public sealed class RefreshToken
{
	private RefreshToken() { }

	public Guid Id { get; private set; }

	public Guid UserId { get; private set; }

	public string TokenHash { get; private set; } = null!;

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset ExpiresAtUtc { get; private set; }

	public DateTimeOffset? RevokedAtUtc { get; private set; }

	public Guid? ReplacedByTokenId { get; private set; }

	public bool IsRevoked => RevokedAtUtc is not null;

	public static RefreshToken Create(
		Guid id,
		Guid userId,
		string tokenHash,
		DateTimeOffset createdAtUtc,
		DateTimeOffset expiresAtUtc) =>
		new()
		{
			Id = id,
			UserId = userId,
			TokenHash = tokenHash,
			CreatedAtUtc = createdAtUtc,
			ExpiresAtUtc = expiresAtUtc
		};

	public bool IsExpired(DateTimeOffset utcNow) => ExpiresAtUtc <= utcNow;
}
