namespace CloudDrive.Domain.Entities;

public class AuthCodeEntity
{
	public int Id { get; set; }
	public string UsernameOrEmail { get; set; } = default!;
	public string Code { get; set; } = default!;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public int FailedAttempts { get; set; }
	public int SentCodeCount { get; set; }
}
