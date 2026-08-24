using Files.Core.Application.Pagination;
using Shared.Kernel.Results;

namespace Files.Core.Application.Abstractions;

public interface IFilesService
{
	Task<Result<InitiateUploadResult>> InitiateUploadAsync(
		Guid ownerId,
		Guid? folderId,
		string originalFileName,
		string contentType,
		long sizeBytes,
		string sha256Declared,
		CancellationToken ct);

	Task<Result<FileSummary>> CompleteUploadAsync(
		Guid ownerId, Guid fileId, IReadOnlyList<BlobUploadedPart> parts, CancellationToken ct);

	Task<Result<FileSummary>> RenameFileAsync(Guid ownerId, Guid fileId, string name, CancellationToken ct);

	Task<Result<FileSummary>> MoveFileAsync(Guid ownerId, Guid fileId, Guid? newFolderId, CancellationToken ct);

	Task<Result<CursorPage<FileSummary>>> ListFilesAsync(
		Guid ownerId, Guid? folderId, string? cursor, int limit, CancellationToken ct);

	Task<Result<string>> GetDownloadUrlAsync(Guid ownerId, Guid fileId, CancellationToken ct);

	Task<Result> DeleteFileAsync(Guid ownerId, Guid fileId, CancellationToken ct);
}
