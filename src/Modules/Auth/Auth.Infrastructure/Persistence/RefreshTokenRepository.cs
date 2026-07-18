using System.Linq.Expressions;
using Auth.Core.Application.Abstractions;
using Auth.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Persistence;

internal sealed class RefreshTokenRepository(AuthDbContext db) : IRefreshTokenRepository
{
	public Task AddAsync(RefreshToken token, CancellationToken ct)
	{
		db.RefreshTokens.Add(token);
		return Task.CompletedTask;
	}

	public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct) =>
		db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

	public async Task<bool> TryRotateAsync(Guid tokenId, RefreshToken newToken, DateTimeOffset revokedAtUtc, CancellationToken ct)
	{
		// Транзакция объединяет немедленный ExecuteUpdateAsync и последующий Add/SaveChangesAsync
		// в одну атомарную операцию: отзыв старого токена и запись нового либо проходят оба, либо ни один.
		await using var transaction = await db.Database.BeginTransactionAsync(ct);

		var rowsAffected = await db.RefreshTokens
			.Where(t => t.Id == tokenId && t.RevokedAtUtc == null && t.ExpiresAtUtc > revokedAtUtc)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(t => t.RevokedAtUtc, revokedAtUtc)
					.SetProperty(t => t.ReplacedByTokenId, newToken.Id),
				ct);

		// Не 1 - значит UPDATE не затронул строку (уже отозвана, истекла или не существует):
		// проигранная гонка с параллельным вызовом - защита от двойной ротации.
		if (rowsAffected != 1)
			return false;

		db.RefreshTokens.Add(newToken);
		await db.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);

		return true;
	}

	public async Task<bool> TryRevokeByHashAsync(string tokenHash, DateTimeOffset revokedAtUtc, CancellationToken ct) =>
		await RevokeWhereAsync(t => t.TokenHash == tokenHash, revokedAtUtc, ct) == 1;

	public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken ct) =>
		RevokeWhereAsync(t => t.UserId == userId, revokedAtUtc, ct);

	public Task RevokeSessionAsync(Guid sessionId, DateTimeOffset revokedAtUtc, CancellationToken ct) =>
		RevokeWhereAsync(t => t.SessionId == sessionId, revokedAtUtc, ct);

	public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

	private Task<int> RevokeWhereAsync(
		Expression<Func<RefreshToken, bool>> predicate, DateTimeOffset revokedAtUtc, CancellationToken ct) =>
		db.RefreshTokens
			.Where(predicate)
			.Where(t => t.RevokedAtUtc == null)
			.ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, revokedAtUtc), ct);
}
