using Shared.Kernel.Results;

namespace Auth.Core.Application.Abstractions;

public interface IAuthService
{
	Task<Result<VerificationCodeSent>> RegisterAsync(string username, string email, string password, CancellationToken ct);

	Task<Result<AuthTokens>> ConfirmRegistrationAsync(string email, string code, CancellationToken ct);

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
