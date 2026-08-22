using Auth.Core.Domain;

namespace Auth.Core.Application.Abstractions;

public interface IPendingPasswordResetRepository
{
	Task<PendingPasswordReset?> GetByUserIdAsync(Guid userId, CancellationToken ct);

	Task AddAsync(PendingPasswordReset reset, CancellationToken ct);

	Task RemoveAsync(PendingPasswordReset reset, CancellationToken ct);

	/// <summary> Массово удаляет просроченные заявки (без загрузки сущностей) - ограничивает рост таблицы без фоновых джоб. </summary>
	Task<int> DeleteExpiredAsync(DateTimeOffset utcNow, CancellationToken ct);

	/// <summary> Удаляет заявку пользователя без предварительной загрузки - для кросс-очистки на успехе конкурирующего флоу. </summary>
	Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken ct);

	Task SaveChangesAsync(CancellationToken ct);
}
