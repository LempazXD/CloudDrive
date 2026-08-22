using Auth.Core.Application.Abstractions;
using Auth.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Persistence;

internal sealed class PendingPasswordChangeRepository(AuthDbContext db) : IPendingPasswordChangeRepository
{
	// Не AsNoTracking: confirm-flow инкрементит AttemptCount на отслеживаемой сущности и сохраняет
	// через SaveChangesAsync - тот же паттерн, что PendingPasswordResetRepository.
	public Task<PendingPasswordChange?> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
		db.PendingPasswordChanges.SingleOrDefaultAsync(p => p.UserId == userId, ct);

	public Task AddAsync(PendingPasswordChange change, CancellationToken ct)
	{
		db.PendingPasswordChanges.Add(change);
		return Task.CompletedTask;
	}

	public Task RemoveAsync(PendingPasswordChange change, CancellationToken ct)
	{
		db.PendingPasswordChanges.Remove(change);
		return Task.CompletedTask;
	}

	public Task<int> DeleteExpiredAsync(DateTimeOffset utcNow, CancellationToken ct) =>
		db.PendingPasswordChanges.Where(p => p.ExpiresAtUtc <= utcNow).ExecuteDeleteAsync(ct);

	public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
