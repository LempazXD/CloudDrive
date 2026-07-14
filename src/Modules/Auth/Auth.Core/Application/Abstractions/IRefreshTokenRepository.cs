using Auth.Core.Domain;

namespace Auth.Core.Application.Abstractions;

public interface IRefreshTokenRepository
{
	Task AddAsync(RefreshToken token, CancellationToken ct);

	Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct);

	/// <summary>
	/// Атомарно отзывает <paramref name="tokenId"/> и сохраняет <paramref name="newToken"/> как его
	/// замену в одной транзакции, но только если токен ещё не отозван (защита от двух параллельных
	/// вызовов обновления, ротирующих один и тот же родительский токен).
	/// Возвращает false, не сохраняя <paramref name="newToken"/>,
	/// если токен уже был отозван параллельным вызовом или не существует.
	/// </summary>
	Task<bool> TryRotateAsync(Guid tokenId, RefreshToken newToken, DateTimeOffset revokedAtUtc, CancellationToken ct);

	/// <summary>
	/// Атомарно отзывает токен по <paramref name="tokenHash"/>.
	/// <para>Возвращает false, если такого токена не существует или он уже был отозван.</para>
	/// </summary>
	Task<bool> TryRevokeByHashAsync(string tokenHash, DateTimeOffset revokedAtUtc, CancellationToken ct);

	/// <summary>
	/// Отзывает все активные refresh-токены пользователя <paramref name="userId"/>.
	/// <para>
	/// Вызывается, когда повторно предъявляется уже отозванный токен - сигнал о возможной краже:
	/// весь набор сессий аннулируется вместо того, чтобы доверять любому токену, произошедшему от него.
	/// </para>
	/// </summary>
	Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken ct);

	Task SaveChangesAsync(CancellationToken ct);
}
