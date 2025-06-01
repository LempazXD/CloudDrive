namespace CloudDrive.Application.DTOs;

public class AuthCodeDto
{
	public int Id { get; set; }
	public string UsernameOrMail { get; set; } = default!;
	public string Code { get; set; } = default!;
	public int FailedAttempts { get; set; }
	public int SentCodeCount { get; set; }
	public DateTime CreatedAt { get; set; }
}
