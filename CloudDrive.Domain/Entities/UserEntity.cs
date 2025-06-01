namespace CloudDrive.Domain.Entities;

public class UserEntity
{
	public int Id { get; set; }
	public string Username { get; set; } = default!;
	public string Password { get; set; } = default!;
	public string Email { get; set; } = default!;
	public DateTime CreatedAt { get; set; }
	public ICollection<FileEntity> Files { get; set; } = new List<FileEntity>();
}
