using Files.Core.Domain;

namespace Files.Core.Application.Abstractions;

public interface IFolderRepository
{
	Task AddAsync(Folder folder, CancellationToken ct);

	Task<Folder?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task<bool> ExistsAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task<bool> HasSubfoldersAsync(Guid folderId, CancellationToken ct);

	Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task SaveChangesAsync(CancellationToken ct);
}
