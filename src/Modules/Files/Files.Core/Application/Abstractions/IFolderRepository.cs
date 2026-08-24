using Files.Core.Domain;

namespace Files.Core.Application.Abstractions;

public interface IFolderRepository
{
	Task AddAsync(Folder folder, CancellationToken ct);

	Task<Folder?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task<bool> ExistsAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task<bool> HasSubfoldersAsync(Guid folderId, CancellationToken ct);

	Task<bool> RenameAsync(Guid id, Guid ownerId, string newName, CancellationToken ct);

	/// <summary>
	/// Перемещает папку под нового родителя (null - в корень). Внутри держит собственную
	/// транзакцию с advisory-lock на владельца и проверкой на цикл - см. реализацию в
	/// Files.Infrastructure/Persistence/FolderRepository.cs и ADR 0016.
	/// </summary>
	Task<FolderMoveOutcome> MoveAsync(Guid id, Guid ownerId, Guid? newParentFolderId, CancellationToken ct);

	Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken ct);

	Task SaveChangesAsync(CancellationToken ct);
}
