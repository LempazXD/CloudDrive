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

	Task MarkCompletedAsync(Guid id, DateTimeOffset nowUtc, CancellationToken ct);

	Task MarkFailedAsync(Guid id, DateTimeOffset nowUtc, CancellationToken ct);

	Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task SaveChangesAsync(CancellationToken ct);
}
