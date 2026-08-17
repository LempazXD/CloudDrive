using Auth.Core.Application.Abstractions;
using Auth.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Persistence;

internal sealed class PendingRegistrationRepository(AuthDbContext db) : IPendingRegistrationRepository
{
	// Не AsNoTracking: в отличие от RefreshToken (мутации только через ExecuteUpdateAsync),
	// confirm-flow инкрементит AttemptCount на отслеживаемой сущности и сохраняет через SaveChangesAsync.
	public Task<PendingRegistration?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct) =>
		db.PendingRegistrations.SingleOrDefaultAsync(p => p.NormalizedEmail == normalizedEmail, ct);

	public Task AddAsync(PendingRegistration registration, CancellationToken ct)
	{
		db.PendingRegistrations.Add(registration);
		return Task.CompletedTask;
	}

	public Task RemoveAsync(PendingRegistration registration, CancellationToken ct)
	{
		db.PendingRegistrations.Remove(registration);
		return Task.CompletedTask;
	}

	public Task<int> DeleteExpiredAsync(DateTimeOffset utcNow, CancellationToken ct) =>
		db.PendingRegistrations.Where(p => p.ExpiresAtUtc <= utcNow).ExecuteDeleteAsync(ct);

	public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
