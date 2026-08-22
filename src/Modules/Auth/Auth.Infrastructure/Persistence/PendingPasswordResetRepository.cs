using Auth.Core.Application.Abstractions;
using Auth.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Persistence;

internal sealed class PendingPasswordResetRepository(AuthDbContext db) : IPendingPasswordResetRepository
{
	// Не AsNoTracking: confirm-flow инкрементит AttemptCount на отслеживаемой сущности и сохраняет
	// через SaveChangesAsync - тот же паттерн, что PendingRegistrationRepository.
	public Task<PendingPasswordReset?> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
		db.PendingPasswordResets.SingleOrDefaultAsync(p => p.UserId == userId, ct);

	public Task AddAsync(PendingPasswordReset reset, CancellationToken ct)
	{
		db.PendingPasswordResets.Add(reset);
		return Task.CompletedTask;
	}

	public Task RemoveAsync(PendingPasswordReset reset, CancellationToken ct)
	{
		db.PendingPasswordResets.Remove(reset);
		return Task.CompletedTask;
	}

	public Task<int> DeleteExpiredAsync(DateTimeOffset utcNow, CancellationToken ct) =>
		db.PendingPasswordResets.Where(p => p.ExpiresAtUtc <= utcNow).ExecuteDeleteAsync(ct);

	public Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken ct) =>
		db.PendingPasswordResets.Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);

	public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
