using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Npgsql;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class RenameFolderAsyncTests
{
	[Fact]
	public async Task RenameFolderAsync_EmptyName_ReturnsInvalidName()
	{
		var harness = new FolderServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(Guid.NewGuid(), Guid.NewGuid(), "   ", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.InvalidName", result.Error!.Code);
		_ = harness.FolderRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RenameFolderAsync_ReservedName_ReturnsInvalidName()
	{
		var harness = new FolderServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(Guid.NewGuid(), Guid.NewGuid(), "..", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.InvalidName", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_ControlCharacter_ReturnsInvalidName()
	{
		var harness = new FolderServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(Guid.NewGuid(), Guid.NewGuid(), "Photos\r\nX-Injected: 1", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.InvalidName", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_BidiFormatCharacter_ReturnsInvalidName()
	{
		// U+202E (RIGHT-TO-LEFT OVERRIDE) - категория Unicode Format, не Control - классический
		// приём подмены видимого расширения файла; не ловится ни char.IsControl, ни IsNullOrWhiteSpace.
		var harness = new FolderServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(Guid.NewGuid(), Guid.NewGuid(), "Photos‮gnp.exe", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.InvalidName", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_QuoteCharacter_ReturnsInvalidName()
	{
		var harness = new FolderServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(Guid.NewGuid(), Guid.NewGuid(), "Photos\"; evil=1", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.InvalidName", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_NameExactly255Characters_Succeeds()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Old", harness.TimeProvider.GetUtcNow());
		var name255 = new string('a', 255);
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.RenameAsync(folder.Id, ownerId, name255, Arg.Any<CancellationToken>()).Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(ownerId, folder.Id, name255, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(name255, result.Value.Name);
	}

	[Fact]
	public async Task RenameFolderAsync_NameOver255Characters_ReturnsNameTooLong()
	{
		var harness = new FolderServiceTestHarness();
		var sut = harness.CreateSut();
		var name256 = new string('a', 256);

		var result = await sut.RenameFolderAsync(Guid.NewGuid(), Guid.NewGuid(), name256, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NameTooLong", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_UnknownFolder_ReturnsNotFound()
	{
		var harness = new FolderServiceTestHarness();
		harness.FolderRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((Folder?)null);
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(Guid.NewGuid(), Guid.NewGuid(), "NewName", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_DeletedBetweenFetchAndWrite_ReturnsNotFound()
	{
		// Регрессия на найденную при проектировании гонку: если папку удалили в промежутке между
		// GetByIdAsync и RenameAsync, bool от RenameAsync должен быть проверен явно, а не
		// отброшен - иначе клиент получил бы 200 на уже несуществующую папку.
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Old", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.RenameAsync(folder.Id, ownerId, "New", Arg.Any<CancellationToken>()).Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(ownerId, folder.Id, "New", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_NameConflict_RawPostgresException_ReturnsConflict()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Old", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository
			.RenameAsync(folder.Id, ownerId, "New", Arg.Any<CancellationToken>())
			.Throws(new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(ownerId, folder.Id, "New", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_NameConflict_WrappedDbUpdateException_ReturnsConflict()
	{
		// Обе формы (сырой PostgresException и DbUpdateException, оборачивающий его) перехватываются
		// намеренно - см. UniqueConstraintExceptionHelper.IsUniqueViolation.
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Old", harness.TimeProvider.GetUtcNow());
		var pgException = new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository
			.RenameAsync(folder.Id, ownerId, "New", Arg.Any<CancellationToken>())
			.Throws(new DbUpdateException("duplicate key value", pgException));
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(ownerId, folder.Id, "New", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFolderAsync_Valid_ReturnsRenamedSummary()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var parentId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, parentId, "Old", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.RenameAsync(folder.Id, ownerId, "New", Arg.Any<CancellationToken>()).Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(ownerId, folder.Id, "New", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(folder.Id, result.Value.Id);
		Assert.Equal(parentId, result.Value.ParentFolderId);
		Assert.Equal("New", result.Value.Name);
		Assert.Equal(folder.CreatedAtUtc, result.Value.CreatedAtUtc);
		_ = harness.FolderRepository.Received(1).RenameAsync(folder.Id, ownerId, "New", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RenameFolderAsync_TrimsWhitespace()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Old", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.RenameAsync(folder.Id, ownerId, "New", Arg.Any<CancellationToken>()).Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(ownerId, folder.Id, "  New  ", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("New", result.Value.Name);
		_ = harness.FolderRepository.Received(1).RenameAsync(folder.Id, ownerId, "New", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RenameFolderAsync_SameNameAfterNormalization_ReturnsSuccessWithoutCallingRenameAsync()
	{
		var harness = new FolderServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		var sut = harness.CreateSut();

		var result = await sut.RenameFolderAsync(ownerId, folder.Id, "  Photos  ", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("Photos", result.Value.Name);
		_ = harness.FolderRepository.DidNotReceive().RenameAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
