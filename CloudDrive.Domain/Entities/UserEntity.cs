namespace CloudDrive.Domain.Entities;

public class UserEntity
{
	public Guid Id { get; private set; }
	public string Username { get; private set; }
	public string Password { get; private set; }
	public string Email { get; private set; }
	public DateTime CreatedAt { get; private set; }
	public ICollection<FileEntity> Files { get; private set; } = new List<FileEntity>();

	private UserEntity() { } // Для EF Core

	private UserEntity(
		string uername,
		string password,
		string email,
		DateTime createdAt)
	{
		Username = uername;
		Password = password;
		Email = email;
		CreatedAt = createdAt;
	}

	// !!! Это не Domain метод?
	public static UserEntity Create(
		string username,
		string password,
		string email)
	{
		// !!! Добавить валидацию?

		return new UserEntity(
			username,
			password,
			email,
			DateTime.UtcNow);
	}

	// Domain methods
}
