using Files.Core.Application.Abstractions;
using Files.Core.Domain;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Guids;
using Shared.Kernel.Results;

namespace Files.Infrastructure.Application;

internal sealed class FolderService(
	IFolderRepository folderRepository,
	IStoredFileRepository storedFileRepository,
	IGuidProvider guidProvider,
	TimeProvider timeProvider,
	ILogger<FolderService> logger) : IFolderService
{
	private const int MaxFolderNameLength = 255;

	public async Task<Result<FolderSummary>> CreateFolderAsync(
		Guid ownerId, Guid? parentFolderId, string name, CancellationToken ct)
	{
		var nameValidation = EntityNameValidator.Validate(
			name, MaxFolderNameLength, "Files.Folder.InvalidName", "Files.Folder.NameTooLong");
		if (nameValidation.IsFailure)
			return nameValidation.Error!;
		var normalizedName = nameValidation.Value;

		if (parentFolderId is { } parentId && !await folderRepository.ExistsAsync(parentId, ownerId, ct))
			return Error.NotFound("Files.Folder.NotFound");

		var folder = Folder.Create(guidProvider.CreateVersion7(), ownerId, parentFolderId, normalizedName, timeProvider.GetUtcNow());

		try
		{
			await folderRepository.AddAsync(folder, ct);
			await folderRepository.SaveChangesAsync(ct);
		}
		catch (Exception ex) when (UniqueConstraintExceptionHelper.IsUniqueViolation(ex))
		{
			// Тот же паттерн, что и в Files.Infrastructure.Application.FilesService.InitiateUploadAsync:
			// уникальный индекс - подстраховка от гонки двух параллельных create с одинаковым
			// именем в одной родительской папке, а не единственная линия защиты.
			logger.LogWarning(
				"Create folder hit a unique-constraint race on name {Name} under parent {ParentFolderId} for owner {OwnerId}.",
				normalizedName, parentFolderId, ownerId);
			return Error.Conflict("Files.Folder.NameConflict");
		}

		logger.LogInformation("Folder {FolderId} created for owner {OwnerId}.", folder.Id, ownerId);

		return Result.Success(new FolderSummary(folder.Id, folder.ParentFolderId, folder.Name, folder.CreatedAtUtc));
	}

	public async Task<Result<FolderSummary>> RenameFolderAsync(Guid ownerId, Guid folderId, string name, CancellationToken ct)
	{
		var nameValidation = EntityNameValidator.Validate(
			name, MaxFolderNameLength, "Files.Folder.InvalidName", "Files.Folder.NameTooLong");
		if (nameValidation.IsFailure)
			return nameValidation.Error!;
		var normalizedName = nameValidation.Value;

		var folder = await folderRepository.GetByIdAsync(folderId, ownerId, ct);
		if (folder is null)
			return Error.NotFound("Files.Folder.NotFound");

		// Имя не изменилось (после нормализации) - успех без записи, не только как оптимизация:
		// так не нужно решать, нарушает ли UPDATE уникальный индекс сам против себя.
		if (folder.Name == normalizedName)
			return Result.Success(new FolderSummary(folder.Id, folder.ParentFolderId, folder.Name, folder.CreatedAtUtc));

		bool renamed;
		try
		{
			renamed = await folderRepository.RenameAsync(folderId, ownerId, normalizedName, ct);
		}
		catch (Exception ex) when (UniqueConstraintExceptionHelper.IsUniqueViolation(ex))
		{
			logger.LogWarning(
				"Rename folder hit a unique-constraint race on name {Name} for folder {FolderId} owned by {OwnerId}.",
				normalizedName, folderId, ownerId);
			return Error.Conflict("Files.Folder.NameConflict");
		}

		// false здесь означает, что папку удалили в промежутке между GetByIdAsync выше и этим
		// вызовом - тоже NotFound, а не молчаливый успех (bool от RenameAsync проверяется
		// явно, в отличие от DeleteFolderAsync ниже, где он сейчас отбрасывается).
		if (!renamed)
			return Error.NotFound("Files.Folder.NotFound");

		logger.LogInformation("Folder {FolderId} renamed for owner {OwnerId}.", folderId, ownerId);

		return Result.Success(new FolderSummary(folder.Id, folder.ParentFolderId, normalizedName, folder.CreatedAtUtc));
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
}
