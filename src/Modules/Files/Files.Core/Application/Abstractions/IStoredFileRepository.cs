using Files.Core.Domain;

namespace Files.Core.Application.Abstractions;

public interface IStoredFileRepository
{
	Task AddAsync(StoredFile file, CancellationToken ct);

	Task<StoredFile?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task<IReadOnlyList<StoredFile>> ListAsync(Guid ownerId, Guid? folderId, Guid? afterId, int limit, CancellationToken ct);

	Task<bool> ExistsInFolderAsync(Guid folderId, CancellationToken ct);

	/// <summary>
	/// Атомарно переводит файл в статус <see cref="FileStatus.Completing"/>, но только если он ещё
	/// в <see cref="FileStatus.Pending"/> либо застрял в <see cref="FileStatus.Completing"/> дольше
	/// <paramref name="staleAfter"/> (защита от двух параллельных вызовов complete на один и тот же
	/// файл и от восстановления после падения процесса на середине предыдущей попытки).
	/// Возвращает false, если ни одно из условий не выполнено.
	/// </summary>
	Task<bool> TryCompleteAsync(Guid id, Guid ownerId, DateTimeOffset nowUtc, TimeSpan staleAfter, CancellationToken ct);

	Task MarkCompletedAsync(Guid id, long actualSizeBytes, DateTimeOffset nowUtc, CancellationToken ct);

	Task MarkFailedAsync(Guid id, DateTimeOffset nowUtc, CancellationToken ct);

	Task<bool> RenameAsync(Guid id, Guid ownerId, string newName, DateTimeOffset nowUtc, CancellationToken ct);

	Task<bool> MoveAsync(Guid id, Guid ownerId, Guid? newFolderId, DateTimeOffset nowUtc, CancellationToken ct);

	Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task<bool> SoftDeleteAsync(Guid id, Guid ownerId, DateTimeOffset nowUtc, CancellationToken ct);

	Task<bool> RestoreAsync(Guid id, Guid ownerId, DateTimeOffset nowUtc, CancellationToken ct);

	Task<IReadOnlyList<StoredFile>> ListTrashAsync(Guid ownerId, Guid? afterId, int limit, CancellationToken ct);

	/// <summary>
	/// Кандидаты на окончательное удаление фоновой очисткой: строки в корзине, чей срок хранения
	/// истёк по состоянию на <paramref name="cutoffUtc"/>. Кросс-пользовательский запрос - вызывается
	/// только системной очисткой, не по запросу пользователя.
	/// </summary>
	Task<IReadOnlyList<StoredFile>> ListExpiredTrashAsync(DateTimeOffset cutoffUtc, int limit, CancellationToken ct);

	/// <summary>
	/// Окончательно удаляет строку, только если она сейчас в корзине - независимо от того, как давно.
	/// Для ручного purge (пользователь явно просит удалить немедленно, минуя срок хранения).
	/// </summary>
	Task<bool> PurgeIfTrashedAsync(Guid id, Guid ownerId, CancellationToken ct);

	/// <summary>
	/// Окончательно удаляет строку, только если она всё ещё в корзине и всё ещё просрочена на момент
	/// записи (не только на момент исходной выборки в <see cref="ListExpiredTrashAsync"/>) - если файл
	/// восстановили, или восстановили и тут же удалили заново, между выборкой батча и этим вызовом,
	/// условие не совпадёт и строка останется нетронутой. Для автоматической фоновой очистки.
	/// </summary>
	Task<bool> PurgeIfStillExpiredAsync(Guid id, Guid ownerId, DateTimeOffset cutoffUtc, CancellationToken ct);

	Task SaveChangesAsync(CancellationToken ct);
}
