using Auth.Core.Domain;

namespace Auth.Core.Application.Abstractions;

public interface IPendingRegistrationRepository
{
	Task<PendingRegistration?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct);

	Task AddAsync(PendingRegistration registration, CancellationToken ct);

	Task RemoveAsync(PendingRegistration registration, CancellationToken ct);

	/// <summary> Массово удаляет просроченные заявки (без загрузки сущностей) - ограничивает рост таблицы без фоновых джоб. </summary>
	Task<int> DeleteExpiredAsync(DateTimeOffset utcNow, CancellationToken ct);

	Task SaveChangesAsync(CancellationToken ct);
}
