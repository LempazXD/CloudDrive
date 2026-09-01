using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Npgsql;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class MoveFileAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	private static StoredFile CreateFile(Guid ownerId, Guid? folderId, string name, DateTimeOffset now) =>
		StoredFile.Create(Guid.NewGuid(), ownerId, folderId, name, "text/plain", 100, ValidSha256, "key", null, 1, now);

	[Fact]
	public async Task MoveFileAsync_UnknownFile_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.StoredFileRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((StoredFile?)null);
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFileAsync_FileInTrash_ReturnsInTrash()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow())
			.SetDeletedAtUtc(harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InTrash", result.Error!.Code);
		_ = harness.StoredFileRepository.DidNotReceive().MoveAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task MoveFileAsync_SameFolder_ReturnsSuccessWithoutCallingMoveAsync()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folderId = Guid.NewGuid();
		var file = CreateFile(ownerId, folderId, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, folderId, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(folderId, result.Value.FolderId);
		_ = harness.StoredFileRepository.DidNotReceive().MoveAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task MoveFileAsync_TargetFolderNotFound_ReturnsFolderNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.FolderRepository.ExistsAsync(Arg.Any<Guid>(), ownerId, Arg.Any<CancellationToken>()).Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFileAsync_ToRoot_SkipsFolderExistenceCheck_Succeeds()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, Guid.NewGuid(), "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.MoveAsync(file.Id, ownerId, null, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, null, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Null(result.Value.FolderId);
		_ = harness.FolderRepository.DidNotReceive().ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task MoveFileAsync_DeletedBetweenFetchAndWrite_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var targetFolderId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.FolderRepository.ExistsAsync(targetFolderId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.StoredFileRepository
			.MoveAsync(file.Id, ownerId, targetFolderId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, targetFolderId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFileAsync_NameConflict_RawPostgresException_ReturnsConflict()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var targetFolderId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.FolderRepository.ExistsAsync(targetFolderId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.StoredFileRepository
			.MoveAsync(file.Id, ownerId, targetFolderId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Throws(new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, targetFolderId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFileAsync_NameConflict_WrappedDbUpdateException_ReturnsConflict()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var targetFolderId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		var pgException = new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.FolderRepository.ExistsAsync(targetFolderId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.StoredFileRepository
			.MoveAsync(file.Id, ownerId, targetFolderId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Throws(new DbUpdateException("duplicate key value", pgException));
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, targetFolderId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFileAsync_TargetFolderVanishedConcurrently_RawForeignKeyViolation_ReturnsFolderNotFound()
	{
		// Целевая папка существовала на ExistsAsync-проверке, но исчезла до записи -
		// FK Restrict на StoredFiles.FolderId отклоняет ExecuteUpdateAsync.
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var targetFolderId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.FolderRepository.ExistsAsync(targetFolderId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.StoredFileRepository
			.MoveAsync(file.Id, ownerId, targetFolderId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Throws(new PostgresException("update or delete violates foreign key constraint", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation));
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, targetFolderId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFileAsync_TargetFolderVanishedConcurrently_WrappedForeignKeyViolation_ReturnsFolderNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var targetFolderId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		var pgException = new PostgresException(
			"update or delete violates foreign key constraint", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation);
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.FolderRepository.ExistsAsync(targetFolderId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.StoredFileRepository
			.MoveAsync(file.Id, ownerId, targetFolderId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Throws(new DbUpdateException("fk violation", pgException));
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, targetFolderId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFileAsync_Valid_ReturnsUpdatedSummaryWithUnchangedFields()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var sourceFolderId = Guid.NewGuid();
		var targetFolderId = Guid.NewGuid();
		var file = CreateFile(ownerId, sourceFolderId, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.FolderRepository.ExistsAsync(targetFolderId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.StoredFileRepository
			.MoveAsync(file.Id, ownerId, targetFolderId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.MoveFileAsync(ownerId, file.Id, targetFolderId, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(file.Id, result.Value.Id);
		Assert.Equal(targetFolderId, result.Value.FolderId);
		Assert.Equal(file.OriginalFileName, result.Value.OriginalFileName);
		Assert.Equal(file.ContentType, result.Value.ContentType);
		Assert.Equal(file.SizeBytes, result.Value.SizeBytes);
		Assert.Equal(file.Status, result.Value.Status);
		Assert.Equal(file.CreatedAtUtc, result.Value.CreatedAtUtc);
		_ = harness.StoredFileRepository.Received(1).MoveAsync(
			file.Id, ownerId, targetFolderId, harness.TimeProvider.GetUtcNow(), Arg.Any<CancellationToken>());
	}
}
