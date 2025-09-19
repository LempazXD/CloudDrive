namespace CloudDrive.Domain.Enums;

public enum MailCodeType
{
	Registration, // Код подтверждения для подтверждения почты при регистрации
	Login, // Код подтверждения для входа по коду
	PasswordRecovery // Код подтверждения для восстановления пароля
}
