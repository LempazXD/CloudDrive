using Amazon.S3;
using Files.Core.Application.Abstractions;
using Files.Core.Application.Pagination;
using Files.Core.Domain;
using Files.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
	IOptions<TrashOptions> trashOptions,
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
		var nameValidation = EntityNameValidator.Validate(
			originalFileName, MaxOriginalFileNameLength, "Files.File.InvalidFileName", "Files.File.FileNameTooLong");
		if (nameValidation.IsFailure)
			return nameValidation.Error!;
		var normalizedFileName = nameValidation.Value;

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
			normalizedFileName,
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
		catch (Exception ex) when (UniqueConstraintExceptionHelper.IsUniqueViolation(ex))
		{
			// Тот же паттерн, что и в Auth.Infrastructure.Application.AuthService.RegisterAsync:
			// уникальный индекс - подстраховка от гонки двух параллельных initiate с одинаковым
			// именем в одной папке, а не единственная линия защиты.
			logger.LogWarning(
				"Initiate upload hit a unique-constraint race on name {OriginalFileName} in folder {FolderId} for owner {OwnerId}.",
				normalizedFileName, folderId, ownerId);
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

		if (file.DeletedAtUtc is not null)
			return Error.Conflict("Files.File.InTrash");

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

		BlobObjectInfo blobInfo;
		try
		{
			blobInfo = await blobStorage.CompleteUploadAsync(file.StorageKey, file.UploadId, parts, ct);
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

		// SizeBytes фиксируется по факту из хранилища, а не по декларации клиента на initiate (как
		// Sha256Declared) - тело PUT/частей ничем не привязано к заявленному sizeBytes, так что это
		// единственный источник истины для реального размера объекта.
		await storedFileRepository.MarkCompletedAsync(fileId, blobInfo.SizeBytes, timeProvider.GetUtcNow(), ct);

		logger.LogInformation("File {FileId} completed for owner {OwnerId}.", fileId, ownerId);

		return Result.Success(new FileSummary(
			file.Id, file.FolderId, file.OriginalFileName, file.ContentType, blobInfo.SizeBytes,
			FileStatus.Completed, file.CreatedAtUtc));
	}

	public async Task<Result<FileSummary>> RenameFileAsync(Guid ownerId, Guid fileId, string name, CancellationToken ct)
	{
		var nameValidation = EntityNameValidator.Validate(
			name, MaxOriginalFileNameLength, "Files.File.InvalidFileName", "Files.File.FileNameTooLong");
		if (nameValidation.IsFailure)
			return nameValidation.Error!;
		var normalizedName = nameValidation.Value;

		var file = await storedFileRepository.GetByIdAsync(fileId, ownerId, ct);
		if (file is null)
			return Error.NotFound("Files.File.NotFound");

		if (file.DeletedAtUtc is not null)
			return Error.Conflict("Files.File.InTrash");

		// Имя не изменилось (после нормализации) - успех без записи: не только оптимизация, но и
		// повод не трогать UpdatedAtUtc, когда фактически ничего не поменялось.
		if (file.OriginalFileName == normalizedName)
			return Result.Success(ToSummary(file));

		var now = timeProvider.GetUtcNow();
		bool renamed;
		try
		{
			renamed = await storedFileRepository.RenameAsync(fileId, ownerId, normalizedName, now, ct);
		}
		catch (Exception ex) when (UniqueConstraintExceptionHelper.IsUniqueViolation(ex))
		{
			logger.LogWarning(
				"Rename file hit a unique-constraint race on name {Name} for file {FileId} owned by {OwnerId}.",
				normalizedName, fileId, ownerId);
			return Error.Conflict("Files.File.NameConflict");
		}

		// false здесь означает, что файл удалили в промежутке между GetByIdAsync выше и этим
		// вызовом - тоже NotFound, а не молчаливый успех.
		if (!renamed)
			return Error.NotFound("Files.File.NotFound");

		logger.LogInformation("File {FileId} renamed for owner {OwnerId}.", fileId, ownerId);

		return Result.Success(new FileSummary(
			file.Id, file.FolderId, normalizedName, file.ContentType, file.SizeBytes, file.Status, file.CreatedAtUtc));
	}

	public async Task<Result<FileSummary>> MoveFileAsync(Guid ownerId, Guid fileId, Guid? newFolderId, CancellationToken ct)
	{
		var file = await storedFileRepository.GetByIdAsync(fileId, ownerId, ct);
		if (file is null)
			return Error.NotFound("Files.File.NotFound");

		if (file.DeletedAtUtc is not null)
			return Error.Conflict("Files.File.InTrash");

		// Папка не изменилась - успех без записи: та же причина, что у RenameFileAsync -
		// не трогать UpdatedAtUtc зря и не думать о самоконфликте с уникальным индексом.
		if (file.FolderId == newFolderId)
			return Result.Success(ToSummary(file));

		if (newFolderId is { } targetFolderId && !await folderRepository.ExistsAsync(targetFolderId, ownerId, ct))
			return Error.NotFound("Files.Folder.NotFound");

		var now = timeProvider.GetUtcNow();
		bool moved;
		try
		{
			moved = await storedFileRepository.MoveAsync(fileId, ownerId, newFolderId, now, ct);
		}
		catch (Exception ex) when (UniqueConstraintExceptionHelper.IsUniqueViolation(ex))
		{
			logger.LogWarning(
				"Move file hit a unique-constraint race on name {Name} for file {FileId} owned by {OwnerId}.",
				file.OriginalFileName, fileId, ownerId);
			return Error.Conflict("Files.File.NameConflict");
		}
		catch (Exception ex) when (UniqueConstraintExceptionHelper.IsForeignKeyViolation(ex))
		{
			// Целевая папка удалена в промежутке между проверкой ExistsAsync выше и записью.
			logger.LogWarning(
				"Move file {FileId} (owner {OwnerId}) hit a foreign-key race: target folder {FolderId} vanished.",
				fileId, ownerId, newFolderId);
			return Error.NotFound("Files.Folder.NotFound");
		}

		// false здесь означает, что файл удалили в промежутке между GetByIdAsync выше и этим
		// вызовом - тоже NotFound, а не молчаливый успех (тот же паттерн, что у RenameFileAsync).
		if (!moved)
			return Error.NotFound("Files.File.NotFound");

		logger.LogInformation("File {FileId} moved for owner {OwnerId}.", fileId, ownerId);

		return Result.Success(new FileSummary(
			file.Id, newFolderId, file.OriginalFileName, file.ContentType, file.SizeBytes, file.Status, file.CreatedAtUtc));
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

		if (file is null)
			return Error.NotFound("Files.File.NotFound");

		if (file.DeletedAtUtc is not null)
			return Error.Conflict("Files.File.InTrash");

		if (file.Status != FileStatus.Completed)
			return Error.NotFound("Files.File.NotFound");

		var url = await blobStorage.GetPresignedDownloadUrlAsync(file.StorageKey, file.OriginalFileName, file.ContentType, ct);
		return Result.Success(url);
	}

	// Мягкое удаление - строка и объект в хранилище остаются нетронутыми до истечения срока хранения
	// (TrashOptions.RetentionPeriod) либо явного PurgeFileAsync. Окончательно удаляет
	// TrashPurgeRecurringJob.
	public async Task<Result> DeleteFileAsync(Guid ownerId, Guid fileId, CancellationToken ct)
	{
		var file = await storedFileRepository.GetByIdAsync(fileId, ownerId, ct);
		if (file is null)
			return Result.Failure(Error.NotFound("Files.File.NotFound"));

		// Уже в корзине - идемпотентный no-op, а не ошибка: повторный DELETE (например, ретрай на
		// клиенте) не должен требовать отдельной обработки и тем более не должен эскалировать до
		// безвозвратного удаления - для этого есть отдельный явный PurgeFileAsync.
		if (file.DeletedAtUtc is not null)
			return Result.Success();

		var now = timeProvider.GetUtcNow();
		var trashed = await storedFileRepository.SoftDeleteAsync(fileId, ownerId, now, ct);
		if (!trashed)
			return Result.Failure(Error.NotFound("Files.File.NotFound")); // гонка: удалили между fetch и записью

		logger.LogInformation("File {FileId} moved to trash for owner {OwnerId}.", fileId, ownerId);

		return Result.Success();
	}

	public async Task<Result<FileSummary>> RestoreFileAsync(Guid ownerId, Guid fileId, CancellationToken ct)
	{
		var file = await storedFileRepository.GetByIdAsync(fileId, ownerId, ct);
		if (file is null)
			return Error.NotFound("Files.File.NotFound");

		if (file.DeletedAtUtc is null)
			return Error.Conflict("Files.File.NotInTrash");

		var now = timeProvider.GetUtcNow();
		bool restored;
		try
		{
			restored = await storedFileRepository.RestoreAsync(fileId, ownerId, now, ct);
		}
		catch (Exception ex) when (UniqueConstraintExceptionHelper.IsUniqueViolation(ex))
		{
			// Пока файл лежал в корзине, его место (OwnerId, FolderId, Name) занял другой активный
			// файл - восстановление в то же имя и папку конфликтует с ним.
			logger.LogWarning(
				"Restore file hit a unique-constraint race on name {Name} for file {FileId} owned by {OwnerId}.",
				file.OriginalFileName, fileId, ownerId);
			return Error.Conflict("Files.File.NameConflict");
		}

		// false здесь означает, что файл окончательно удалён (purge) в промежутке между GetByIdAsync
		// выше и этим вызовом - тоже NotFound, тот же паттерн, что у RenameAsync/MoveAsync.
		if (!restored)
			return Error.NotFound("Files.File.NotFound");

		logger.LogInformation("File {FileId} restored from trash for owner {OwnerId}.", fileId, ownerId);

		// Строится из уже прочитанного file, без повторного fetch - та же причина, что у
		// RenameFileAsync/MoveFileAsync: повторное чтение после записи может словить гонку с
		// параллельным удалением и превратить успешное восстановление в ложный NotFound.
		return Result.Success(ToSummary(file) with { DeletedAtUtc = null, PurgeAtUtc = null });
	}

	public async Task<Result<CursorPage<FileSummary>>> ListTrashAsync(Guid ownerId, string? cursor, int limit, CancellationToken ct)
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

		var effectiveLimit = Math.Min(limit, MaxPageSize);
		var files = await storedFileRepository.ListTrashAsync(ownerId, afterId, effectiveLimit + 1, ct);

		var hasMore = files.Count > effectiveLimit;
		var page = hasMore ? files.Take(effectiveLimit).ToList() : files;
		var nextCursor = hasMore ? Cursor.Encode(page[^1].Id) : null;

		return Result.Success(new CursorPage<FileSummary>(page.Select(ToSummary).ToList(), nextCursor));
	}

	public async Task<Result> PurgeFileAsync(Guid ownerId, Guid fileId, CancellationToken ct)
	{
		var file = await storedFileRepository.GetByIdAsync(fileId, ownerId, ct);
		if (file is null)
			return Result.Failure(Error.NotFound("Files.File.NotFound"));

		if (file.DeletedAtUtc is null)
			return Result.Failure(Error.Conflict("Files.File.NotInTrash"));

		// Условное удаление строки сначала, блоб - только если оно реально её затронуло: если файл
		// восстановили в промежутке между fetch и этим вызовом, блоб трогать нельзя, иначе строка
		// осталась бы "активной", указывая на уже стёртый объект.
		var purged = await storedFileRepository.PurgeIfTrashedAsync(fileId, ownerId, ct);
		if (!purged)
			return Result.Failure(Error.NotFound("Files.File.NotFound"));

		await blobStorage.DeleteObjectAsync(file.StorageKey, ct);

		logger.LogInformation("File {FileId} permanently purged for owner {OwnerId}.", fileId, ownerId);

		return Result.Success();
	}

	private FileSummary ToSummary(StoredFile file) => new(
		file.Id, file.FolderId, file.OriginalFileName, file.ContentType, file.SizeBytes, file.Status, file.CreatedAtUtc,
		file.DeletedAtUtc, file.DeletedAtUtc?.Add(trashOptions.Value.RetentionPeriod));

	private static bool IsValidSha256(string? value) =>
		value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
