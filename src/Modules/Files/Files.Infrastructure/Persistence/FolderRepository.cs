using Files.Core.Application.Abstractions;
using Files.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Files.Infrastructure.Persistence;

internal sealed class FolderRepository(FilesDbContext db) : IFolderRepository
{
	public Task AddAsync(Folder folder, CancellationToken ct)
	{
		db.Folders.Add(folder);
		return Task.CompletedTask;
	}

	public Task<Folder?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		db.Folders.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId, ct);

	public Task<bool> ExistsAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		db.Folders.AsNoTracking().AnyAsync(f => f.Id == id && f.OwnerId == ownerId, ct);

	public Task<bool> HasSubfoldersAsync(Guid folderId, CancellationToken ct) =>
		db.Folders.AsNoTracking().AnyAsync(f => f.ParentFolderId == folderId, ct);

	public async Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		await db.Folders
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteDeleteAsync(ct) == 1;

	public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
