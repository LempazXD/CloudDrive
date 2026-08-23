using Shared.Kernel.Results;

namespace Files.Core.Application.Abstractions;

public interface IFolderService
{
	Task<Result<FolderSummary>> CreateFolderAsync(Guid ownerId, Guid? parentFolderId, string name, CancellationToken ct);

	Task<Result<FolderSummary>> RenameFolderAsync(Guid ownerId, Guid folderId, string name, CancellationToken ct);

	Task<Result<FolderSummary>> GetFolderAsync(Guid ownerId, Guid folderId, CancellationToken ct);

	Task<Result> DeleteFolderAsync(Guid ownerId, Guid folderId, CancellationToken ct);
}
