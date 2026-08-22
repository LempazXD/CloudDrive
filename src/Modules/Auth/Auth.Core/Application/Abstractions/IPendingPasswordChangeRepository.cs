using Auth.Core.Domain;

namespace Auth.Core.Application.Abstractions;

public interface IPendingPasswordChangeRepository
{
	Task<PendingPasswordChange?> GetByUserIdAsync(Guid userId, CancellationToken ct);

	Task AddAsync(PendingPasswordChange change, CancellationToken ct);

	Task RemoveAsync(PendingPasswordChange change, CancellationToken ct);

	/// <summary> Массово удаляет просроченные заявки (без загрузки сущностей) - ограничивает рост таблицы без фоновых джоб. </summary>
	Task<int> DeleteExpiredAsync(DateTimeOffset utcNow, CancellationToken ct);

	Task SaveChangesAsync(CancellationToken ct);
}
