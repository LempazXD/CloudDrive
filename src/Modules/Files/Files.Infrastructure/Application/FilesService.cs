using Amazon.S3;
using Files.Core.Application.Abstractions;
using Files.Core.Application.Pagination;
using Files.Core.Domain;
using Files.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Shared.Kernel.Guids;
using Shared.Kernel.Results;

namespace Files.Infrastructure.Application;

internal sealed class FilesService(
	IStoredFileRepository storedFileRepository,
	IFolderRepository folderRepository,
	IBlobStorage blobStorage,
	IGuidProvider guidProvider,
	TimeProvider timeProvider,
	IOptions<ObjectStorageOptions> objectStorageOptions,
	ILogger<FilesService> logger) : IFilesService
{
	private const int MaxPageSize = 100;
	private const int MaxOriginalFileNameLength = 255;

	public async Task<Result<InitiateUploadResult>> InitiateUploadAsync(
		Guid ownerId,
		Guid? folderId,
		string originalFileName,
		string contentType,
		long sizeBytes,
		string sha256Declared,
		CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(originalFileName))
			return Error.Validation("Files.File.InvalidFileName");

		if (originalFileName.Length > MaxOriginalFileNameLength)
			return Error.Validation("Files.File.FileNameTooLong");

		if (string.IsNullOrWhiteSpace(contentType))
			return Error.Validation("Files.File.InvalidContentType");

		if (sizeBytes <= 0)
			return Error.Validation("Files.File.InvalidSize");

		if (!IsValidSha256(sha256Declared))
			return Error.Validation("Files.File.InvalidChecksum");

		if (folderId is { } parentId && !await folderRepository.ExistsAsync(parentId, ownerId, ct))
			return Error.NotFound("Files.Folder.NotFound");

		var now = timeProvider.GetUtcNow();
		var fileId = guidProvider.CreateVersion7();
		var storageKey = $"{ownerId}/{fileId}";

		var uploadTarget = await blobStorage.InitiateUploadAsync(storageKey, contentType, sizeBytes, ct);

		var file = StoredFile.Create(
			fileId,
			ownerId,
			folderId,
			originalFileName,
			contentType,
			sizeBytes,
			sha256Declared,
			storageKey,
			uploadTarget.UploadId,
			uploadTarget.Parts.Count,
			now);

		try
		{
			await storedFileRepository.AddAsync(file, ct);
			await storedFileRepository.SaveChangesAsync(ct);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
		{
			// Тот же паттерн, что и в Auth.Infrastructure.Application.AuthService.RegisterAsync:
			// уникальный индекс - подстраховка от гонки двух параллельных initiate с одинаковым
			// именем в одной папке, а не единственная линия защиты.
			logger.LogWarning(
				"Initiate upload hit a unique-constraint race on name {OriginalFileName} in folder {FolderId} for owner {OwnerId}.",
				originalFileName, folderId, ownerId);
			return Error.Conflict("Files.File.NameConflict");
		}

		logger.LogInformation(
			"File {FileId} initiated for owner {OwnerId} ({SizeBytes} bytes, {PartCount} part(s)).",
			fileId, ownerId, sizeBytes, uploadTarget.Parts.Count);

		return Result.Success(new InitiateUploadResult(fileId, uploadTarget.UploadId, uploadTarget.Parts));
	}

	public async Task<Result<FileSummary>> CompleteUploadAsync(
		Guid ownerId, Guid fileId, IReadOnlyList<BlobUploadedPart> parts, CancellationToken ct)
	{
		var file = await storedFileRepository.GetByIdAsync(fileId, ownerId, ct);
		if (file is null)
			return Error.NotFound("Files.File.NotFound");

		if (file.UploadId is not null && parts.Count != file.ExpectedPartCount)
		{
			logger.LogWarning(
				"Complete upload for file {FileId} (owner {OwnerId}) reported {ReportedParts} part(s), expected {ExpectedParts}.",
				fileId, ownerId, parts.Count, file.ExpectedPartCount);
			return Error.Validation("Files.File.ChecksumMismatch");
		}

		var now = timeProvider.GetUtcNow();
		var claimed = await storedFileRepository.TryCompleteAsync(
			fileId, ownerId, now, objectStorageOptions.Value.CompletionStaleAfter, ct);

		if (!claimed)
		{
			logger.LogWarning(
				"Complete upload race lost for file {FileId} (owner {OwnerId}): already completing/completed elsewhere.",
				fileId, ownerId);
			return Error.Conflict("Files.File.AlreadyCompleted");
		}

		try
		{
			await blobStorage.CompleteUploadAsync(file.StorageKey, file.UploadId, parts, ct);
		}
		catch (AmazonS3Exception ex)
		{
			// Хранилище отклонило complete (например, часть не была реально загружена или ETag не
			// совпал) - это ожидаемый сбой на стороне клиента, а не инфраструктурная авария, поэтому
			// переводим файл в Failed и возвращаем чистую ошибку, а не даём 500 всплыть наверх.
			logger.LogWarning(ex, "Storage rejected complete for file {FileId} (owner {OwnerId}).", fileId, ownerId);
			await storedFileRepository.MarkFailedAsync(fileId, timeProvider.GetUtcNow(), ct);
			return Error.Conflict("Files.File.CompletionFailed");
		}

		await storedFileRepository.MarkCompletedAsync(fileId, timeProvider.GetUtcNow(), ct);

		logger.LogInformation("File {FileId} completed for owner {OwnerId}.", fileId, ownerId);

		return Result.Success(new FileSummary(
			file.Id, file.FolderId, file.OriginalFileName, file.ContentType, file.SizeBytes,
			FileStatus.Completed, file.CreatedAtUtc));
	}

	public async Task<Result<CursorPage<FileSummary>>> ListFilesAsync(
		Guid ownerId, Guid? folderId, string? cursor, int limit, CancellationToken ct)
	{
		if (limit <= 0)
			return Error.Validation("Files.File.InvalidPageSize");

		Guid? afterId = null;
		if (!string.IsNullOrEmpty(cursor))
		{
			if (!Cursor.TryDecode(cursor, out var decoded))
				return Error.Validation("Files.File.InvalidCursor");
			afterId = decoded;
		}

		if (folderId is { } parentId && !await folderRepository.ExistsAsync(parentId, ownerId, ct))
			return Error.NotFound("Files.Folder.NotFound");

		var effectiveLimit = Math.Min(limit, MaxPageSize);
		var files = await storedFileRepository.ListAsync(ownerId, folderId, afterId, effectiveLimit + 1, ct);

		var hasMore = files.Count > effectiveLimit;
		var page = hasMore ? files.Take(effectiveLimit).ToList() : files;
		var nextCursor = hasMore ? Cursor.Encode(page[^1].Id) : null;

		return Result.Success(new CursorPage<FileSummary>(page.Select(ToSummary).ToList(), nextCursor));
	}

	public async Task<Result<string>> GetDownloadUrlAsync(Guid ownerId, Guid fileId, CancellationToken ct)
	{
		var file = await storedFileRepository.GetByIdAsync(fileId, ownerId, ct);

		if (file is null || file.Status != FileStatus.Completed)
			return Error.NotFound("Files.File.NotFound");

		var url = await blobStorage.GetPresignedDownloadUrlAsync(file.StorageKey, file.OriginalFileName, file.ContentType, ct);
		return Result.Success(url);
	}

	// TODO: корзина для удалённых файлов не реализована - удаление необратимо
	public async Task<Result> DeleteFileAsync(Guid ownerId, Guid fileId, CancellationToken ct)
	{
		var file = await storedFileRepository.GetByIdAsync(fileId, ownerId, ct);
		if (file is null)
			return Result.Failure(Error.NotFound("Files.File.NotFound"));

		// Сначала удаляем объект в хранилище, затем строку в БД. Если DB-удаление
		// упадёт, останется мёртвая строка, указывающая на уже удалённый объект
		await blobStorage.DeleteObjectAsync(file.StorageKey, ct);
		await storedFileRepository.DeleteAsync(fileId, ownerId, ct);

		logger.LogInformation("File {FileId} deleted for owner {OwnerId}.", fileId, ownerId);

		return Result.Success();
	}

	public async Task<Result<FolderSummary>> CreateFolderAsync(
		Guid ownerId, Guid? parentFolderId, string name, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(name))
			return Error.Validation("Files.Folder.InvalidName");

		if (parentFolderId is { } parentId && !await folderRepository.ExistsAsync(parentId, ownerId, ct))
			return Error.NotFound("Files.Folder.NotFound");

		var folder = Folder.Create(guidProvider.CreateVersion7(), ownerId, parentFolderId, name, timeProvider.GetUtcNow());

		try
		{
			await folderRepository.AddAsync(folder, ct);
			await folderRepository.SaveChangesAsync(ct);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
		{
			logger.LogWarning(
				"Create folder hit a unique-constraint race on name {Name} under parent {ParentFolderId} for owner {OwnerId}.",
				name, parentFolderId, ownerId);
			return Error.Conflict("Files.Folder.NameConflict");
		}

		logger.LogInformation("Folder {FolderId} created for owner {OwnerId}.", folder.Id, ownerId);

		return Result.Success(new FolderSummary(folder.Id, folder.ParentFolderId, folder.Name, folder.CreatedAtUtc));
	}

	public async Task<Result<FolderSummary>> GetFolderAsync(Guid ownerId, Guid folderId, CancellationToken ct)
	{
		var folder = await folderRepository.GetByIdAsync(folderId, ownerId, ct);
		if (folder is null)
			return Error.NotFound("Files.Folder.NotFound");

		return Result.Success(new FolderSummary(folder.Id, folder.ParentFolderId, folder.Name, folder.CreatedAtUtc));
	}

	// TODO: каскадное удаление содержимого папки не реализовано - удаление блокируется, если не пуста
	public async Task<Result> DeleteFolderAsync(Guid ownerId, Guid folderId, CancellationToken ct)
	{
		var folder = await folderRepository.GetByIdAsync(folderId, ownerId, ct);
		if (folder is null)
			return Result.Failure(Error.NotFound("Files.Folder.NotFound"));

		if (await folderRepository.HasSubfoldersAsync(folderId, ct) || await storedFileRepository.ExistsInFolderAsync(folderId, ct))
			return Result.Failure(Error.Conflict("Files.Folder.NotEmpty"));

		await folderRepository.DeleteAsync(folderId, ownerId, ct);

		logger.LogInformation("Folder {FolderId} deleted for owner {OwnerId}.", folderId, ownerId);

		return Result.Success();
	}

	private static FileSummary ToSummary(StoredFile file) => new(
		file.Id, file.FolderId, file.OriginalFileName, file.ContentType, file.SizeBytes, file.Status, file.CreatedAtUtc);

	private static bool IsValidSha256(string? value) =>
		value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
