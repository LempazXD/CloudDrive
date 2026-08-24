using Files.Core.Application.Abstractions;
using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Npgsql;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class MoveFolderAsyncTests
{
	[Fact]
	public async Task MoveFolderAsync_UnknownFolder_ReturnsNotFound()
	{
		var harness = new FolderServiceTestHarness();
		harness.FolderRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((Folder?)null);
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFolderAsync_SameParent_ReturnsSuccessWithoutCallingMoveAsync()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var parentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, parentId, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, parentId, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(parentId, result.Value.ParentFolderId);
		_ = harness.FolderRepository.DidNotReceive().MoveAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task MoveFolderAsync_IntoSelf_ReturnsCircularMoveWithoutCallingRepository()
	{
		// Дешёвая самопроверка в сервисе должна отсечь этот случай без похода в БД -
		// FolderRepository.MoveAsync поймал бы его и так через CTE, но незачем платить за
		// поход в БД ради самого частого случайного случая (повторный клик/дабл-сабмит).
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var parentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, parentId, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, folder.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.CircularMove", result.Error!.Code);
		_ = harness.FolderRepository.DidNotReceive().ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		_ = harness.FolderRepository.DidNotReceive().MoveAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task MoveFolderAsync_TargetParentNotFound_ReturnsFolderNotFound()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.ExistsAsync(Arg.Any<Guid>(), ownerId, Arg.Any<CancellationToken>()).Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFolderAsync_ToRoot_SkipsExistenceAndSelfChecks_Succeeds()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, Guid.NewGuid(), "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.MoveAsync(folder.Id, ownerId, null, Arg.Any<CancellationToken>())
			.Returns(FolderMoveOutcome.Moved);
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, null, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Null(result.Value.ParentFolderId);
		_ = harness.FolderRepository.DidNotReceive().ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task MoveFolderAsync_RepositoryReportsWouldCreateCycle_ReturnsCircularMove()
	{
		// Настоящее обнаружение цикла (папка в собственного потомка) проверяется только
		// интеграционными тестами против реального Postgres - здесь репозиторий замокан и
		// проверяется только то, что FolderService правильно транслирует его исход в Result.
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var newParentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.ExistsAsync(newParentId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.FolderRepository.MoveAsync(folder.Id, ownerId, newParentId, Arg.Any<CancellationToken>())
			.Returns(FolderMoveOutcome.WouldCreateCycle);
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, newParentId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.CircularMove", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFolderAsync_RepositoryReportsNotFound_ReturnsNotFound()
	{
		// Гонка: папку удалили между GetByIdAsync в сервисе и записью внутри MoveAsync.
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var newParentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.ExistsAsync(newParentId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.FolderRepository.MoveAsync(folder.Id, ownerId, newParentId, Arg.Any<CancellationToken>())
			.Returns(FolderMoveOutcome.NotFound);
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, newParentId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFolderAsync_NameConflict_RawPostgresException_ReturnsConflict()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var newParentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.ExistsAsync(newParentId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.FolderRepository
			.MoveAsync(folder.Id, ownerId, newParentId, Arg.Any<CancellationToken>())
			.Throws(new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, newParentId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFolderAsync_NameConflict_WrappedDbUpdateException_ReturnsConflict()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var newParentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		var pgException = new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.ExistsAsync(newParentId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.FolderRepository
			.MoveAsync(folder.Id, ownerId, newParentId, Arg.Any<CancellationToken>())
			.Throws(new DbUpdateException("duplicate key value", pgException));
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, newParentId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFolderAsync_TargetParentVanishedConcurrently_RawForeignKeyViolation_ReturnsFolderNotFound()
	{
		// Целевой родитель существовал на ExistsAsync-проверке, но исчез до записи -
		// FK Restrict на Folders.ParentFolderId отклоняет ExecuteUpdateAsync внутри MoveAsync.
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var newParentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.ExistsAsync(newParentId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.FolderRepository
			.MoveAsync(folder.Id, ownerId, newParentId, Arg.Any<CancellationToken>())
			.Throws(new PostgresException(
				"update or delete violates foreign key constraint", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation));
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, newParentId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFolderAsync_TargetParentVanishedConcurrently_WrappedForeignKeyViolation_ReturnsFolderNotFound()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var newParentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		var pgException = new PostgresException(
			"update or delete violates foreign key constraint", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation);
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.ExistsAsync(newParentId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.FolderRepository
			.MoveAsync(folder.Id, ownerId, newParentId, Arg.Any<CancellationToken>())
			.Throws(new DbUpdateException("fk violation", pgException));
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, newParentId, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task MoveFolderAsync_Valid_ReturnsUpdatedSummary()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var newParentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.ExistsAsync(newParentId, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		harness.FolderRepository.MoveAsync(folder.Id, ownerId, newParentId, Arg.Any<CancellationToken>())
			.Returns(FolderMoveOutcome.Moved);
		var sut = harness.CreateSut();

		var result = await sut.MoveFolderAsync(ownerId, folder.Id, newParentId, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(folder.Id, result.Value.Id);
		Assert.Equal(newParentId, result.Value.ParentFolderId);
		Assert.Equal(folder.Name, result.Value.Name);
		Assert.Equal(folder.CreatedAtUtc, result.Value.CreatedAtUtc);
		_ = harness.FolderRepository.Received(1).MoveAsync(folder.Id, ownerId, newParentId, Arg.Any<CancellationToken>());
	}
}
