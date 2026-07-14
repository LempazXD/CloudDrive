using Shared.Kernel.Results;

namespace Auth.Core.Application.Abstractions;

public interface IAuthService
{
	Task<Result<AuthUserSummary>> RegisterAsync(string username, string email, string password, CancellationToken ct);

	Task<Result<AuthTokens>> LoginAsync(string login, string password, CancellationToken ct);

	Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken ct);

	Task<Result> LogoutAsync(string refreshToken, CancellationToken ct);
}
