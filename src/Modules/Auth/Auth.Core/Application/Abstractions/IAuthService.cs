using Shared.Kernel.Results;

namespace Auth.Core.Application.Abstractions;

public interface IAuthService
{
	Task<Result<VerificationCodeSent>> RegisterAsync(string username, string email, string password, CancellationToken ct);

	Task<Result<AuthTokens>> ConfirmRegistrationAsync(string email, string code, CancellationToken ct);

	/// <summary>
	/// Инициирует восстановление забытого пароля: если аккаунт с таким email существует, высылает
	/// на него код подтверждения. Ответ намеренно одинаков независимо от того, найден аккаунт или
	/// нет - раскрывать существование аккаунта по email нельзя (в отличие от <see cref="RegisterAsync"/>).
	/// </summary>
	Task<Result<VerificationCodeSent>> ForgotPasswordAsync(string email, CancellationToken ct);

	/// <summary>
	/// Завершает восстановление пароля по коду, высланному <see cref="ForgotPasswordAsync"/>:
	/// подтверждает код и устанавливает новый пароль. Успех отзывает все текущие сессии
	/// пользователя и сразу выдаёт новую пару токенов - отдельный вызов <see cref="LoginAsync"/> не нужен.
	/// </summary>
	Task<Result<AuthTokens>> ResetPasswordAsync(
		string email, string code, string newPassword, string confirmNewPassword, CancellationToken ct);

	/// <summary>
	/// Меняет пароль уже аутентифицированного пользователя по его текущему паролю. Успех отзывает
	/// все текущие сессии пользователя и сразу выдаёт новую пару токенов.
	/// </summary>
	Task<Result<AuthTokens>> ChangePasswordAsync(
		Guid userId, string currentPassword, string newPassword, string confirmNewPassword, CancellationToken ct);

	Task<Result<AuthTokens>> LoginAsync(string login, string password, CancellationToken ct);

	Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken ct);

	Task<Result> LogoutAsync(string refreshToken, CancellationToken ct);

	/// <summary>
	/// Выходит из всех сессий пользователя, определяемого по <paramref name="refreshToken"/>.
	/// Если <paramref name="keepCurrentSession"/> = true, сессия предъявленного токена не отзывается
	/// (опция "остаться на текущем устройстве"); иначе отзываются все сессии без исключения.
	/// </summary>
	Task<Result> LogoutAllAsync(string refreshToken, bool keepCurrentSession, CancellationToken ct);
}
