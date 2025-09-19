using CloudDrive.Domain.Enums;

namespace CloudDrive.Domain.Entities;

public class TemporaryRegistrationDataEntity // !!! Мб поменять название
{
	private const int tempregdatalifetime = 15; // !!! Поменять название // !!! Переместить?

	public Guid Id { get; private set; }
	public string Username { get; private set; }
	public string Password { get; private set; }
	public string Email { get; private set; }
	public DateTime ExpiresAt { get; private set; } // 15 минут lifetime

	private TemporaryRegistrationDataEntity(
	string username,
	string password,
	string email,
	DateTime expiresAt)
	{
		Username = username;
		Password = password;
		Email = email;
		ExpiresAt = expiresAt;
	}

	public static TemporaryRegistrationDataEntity Create(
	string username,
	string password,
	string email)
	{
		// !!! Добавить валидацию?

		return new TemporaryRegistrationDataEntity(
			username,
			password,
			email,
			DateTime.UtcNow.AddMinutes(tempregdatalifetime));
	}
}
