namespace CloudDrive.Application.DTOs;

public class UserDto
{
	public int Id { get; set; }
	public string Username { get; set; } = default!;
	public string Email { get; set; } = default!;
	public DateTime CreatedAt { get; set; }
}
