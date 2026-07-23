using Files.Core.Application.Abstractions;
using Files.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Files.Infrastructure.Persistence;

internal sealed class StoredFileRepository(FilesDbContext db) : IStoredFileRepository
{
	public Task AddAsync(StoredFile file, CancellationToken ct)
	{
		db.StoredFiles.Add(file);
		return Task.CompletedTask;
	}

	public Task<StoredFile?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId, ct);

	public async Task<IReadOnlyList<StoredFile>> ListAsync(
		Guid ownerId, Guid? folderId, Guid? afterId, int limit, CancellationToken ct)
	{
		var query = db.StoredFiles.AsNoTracking()
			.Where(f => f.OwnerId == ownerId && f.FolderId == folderId);

		if (afterId is { } cursor)
			query = query.Where(f => f.Id > cursor);

		return await query.OrderBy(f => f.Id).Take(limit).ToListAsync(ct);
	}

	public Task<bool> ExistsInFolderAsync(Guid folderId, CancellationToken ct) =>
		db.StoredFiles.AsNoTracking().AnyAsync(f => f.FolderId == folderId, ct);

	public async Task<bool> TryCompleteAsync(
		Guid id, Guid ownerId, DateTimeOffset nowUtc, TimeSpan staleAfter, CancellationToken ct)
	{
		var staleThreshold = nowUtc - staleAfter;

		var rowsAffected = await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.Where(f => f.Status == FileStatus.Pending
				|| (f.Status == FileStatus.Completing && f.UpdatedAtUtc < staleThreshold))
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(f => f.Status, FileStatus.Completing)
					.SetProperty(f => f.UpdatedAtUtc, nowUtc),
				ct);

		// Не 1 - UPDATE не затронул строку: проигранная гонка с
		// параллельным complete - защита от двойного завершения.
		return rowsAffected == 1;
	}

	public async Task MarkCompletedAsync(Guid id, DateTimeOffset nowUtc, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.Status == FileStatus.Completing)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(f => f.Status, FileStatus.Completed)
					.SetProperty(f => f.UpdatedAtUtc, nowUtc),
				ct);

	public async Task MarkFailedAsync(Guid id, DateTimeOffset nowUtc, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.Status == FileStatus.Completing)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(f => f.Status, FileStatus.Failed)
					.SetProperty(f => f.UpdatedAtUtc, nowUtc),
				ct);

	public async Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteDeleteAsync(ct) == 1;

	public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
