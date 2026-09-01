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
			.Where(f => f.OwnerId == ownerId && f.FolderId == folderId && f.DeletedAtUtc == null);

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

	public async Task MarkCompletedAsync(Guid id, long actualSizeBytes, DateTimeOffset nowUtc, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.Status == FileStatus.Completing)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(f => f.Status, FileStatus.Completed)
					.SetProperty(f => f.SizeBytes, actualSizeBytes)
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

	public async Task<bool> RenameAsync(Guid id, Guid ownerId, string newName, DateTimeOffset nowUtc, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(f => f.OriginalFileName, newName)
					.SetProperty(f => f.UpdatedAtUtc, nowUtc),
				ct) == 1;

	public async Task<bool> MoveAsync(Guid id, Guid ownerId, Guid? newFolderId, DateTimeOffset nowUtc, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(f => f.FolderId, newFolderId)
					.SetProperty(f => f.UpdatedAtUtc, nowUtc),
				ct) == 1;

	public async Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteDeleteAsync(ct) == 1;

	public async Task<bool> SoftDeleteAsync(Guid id, Guid ownerId, DateTimeOffset nowUtc, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(f => f.DeletedAtUtc, nowUtc)
					.SetProperty(f => f.UpdatedAtUtc, nowUtc),
				ct) == 1;

	public async Task<bool> RestoreAsync(Guid id, Guid ownerId, DateTimeOffset nowUtc, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(f => f.DeletedAtUtc, (DateTimeOffset?)null)
					.SetProperty(f => f.UpdatedAtUtc, nowUtc),
				ct) == 1;

	public async Task<IReadOnlyList<StoredFile>> ListTrashAsync(Guid ownerId, Guid? afterId, int limit, CancellationToken ct)
	{
		var query = db.StoredFiles.AsNoTracking()
			.Where(f => f.OwnerId == ownerId && f.DeletedAtUtc != null);

		if (afterId is { } cursor)
			query = query.Where(f => f.Id > cursor);

		return await query.OrderBy(f => f.Id).Take(limit).ToListAsync(ct);
	}

	public async Task<IReadOnlyList<StoredFile>> ListExpiredTrashAsync(DateTimeOffset cutoffUtc, int limit, CancellationToken ct) =>
		await db.StoredFiles.AsNoTracking()
			.Where(f => f.DeletedAtUtc != null && f.DeletedAtUtc <= cutoffUtc)
			.OrderBy(f => f.DeletedAtUtc)
			.Take(limit)
			.ToListAsync(ct);

	public async Task<bool> PurgeIfTrashedAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId && f.DeletedAtUtc != null)
			.ExecuteDeleteAsync(ct) == 1;

	public async Task<bool> PurgeIfStillExpiredAsync(Guid id, Guid ownerId, DateTimeOffset cutoffUtc, CancellationToken ct) =>
		await db.StoredFiles
			.Where(f => f.Id == id && f.OwnerId == ownerId && f.DeletedAtUtc != null && f.DeletedAtUtc <= cutoffUtc)
			.ExecuteDeleteAsync(ct) == 1;

	public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
