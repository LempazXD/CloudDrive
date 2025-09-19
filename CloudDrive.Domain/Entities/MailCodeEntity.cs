using CloudDrive.Domain.Enums;

namespace CloudDrive.Domain.Entities;

public class MailCodeEntity
{
	public Guid Id { get; private set; }
	public string Email { get; private set; }
	public string? Code { get; private set; } // !!! Хранить ли в зашифрованном виде?
	public MailCodeType Type { get; set; }
	public DateTime CreatedAt { get; private set; }
	public int FailedAttempts { get; private set; }
	public int SentCodeCount { get; private set; }
	public string IpAddress { get; private set; } // !!! надо ли?
	public string UserAgent { get; private set; } // !!! надо ли?

	private MailCodeEntity() { } // Для EF Core

	private MailCodeEntity(
		string email,
		string code,
		MailCodeType type,
		DateTime createdAt,
		int failedAttempts,
		int sentCodeCount,
		string ipAdress,
		string userAgent)
	{
		Email = email;
		Code = code;
		Type = type;
		CreatedAt = createdAt;
		FailedAttempts = failedAttempts;
		SentCodeCount = sentCodeCount;
		IpAddress = ipAdress;
		UserAgent = userAgent;
	}

	public static MailCodeEntity Create(
		string email,
		string code,
		MailCodeType type,
		string ipAdress,
		string userAgent)
	{
		// !!! Добавить валидацию?

		return new MailCodeEntity(
			email,
			code,
			type,
			DateTime.UtcNow,
			0,
			0, // !!! Или 1? Проверить
			ipAdress,
			userAgent);
	}

	// --- Domain methods ---
	public void NewCode(string newCode) // !!! Поменять название
	{
		Code = newCode;
		CreatedAt = DateTime.UtcNow;
		FailedAttempts = 0;
		SentCodeCount++;
	}

	public void WrongCode() // !!! Поменять название
	{
		FailedAttempts++;
	}

	public void ResetCode()
	{
		Code = null;
	}
}
