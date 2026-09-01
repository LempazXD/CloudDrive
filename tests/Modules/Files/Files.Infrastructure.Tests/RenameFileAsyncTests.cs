using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Npgsql;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class RenameFileAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	private static StoredFile CreateFile(Guid ownerId, Guid? folderId, string name, DateTimeOffset now) =>
		StoredFile.Create(Guid.NewGuid(), ownerId, folderId, name, "text/plain", 100, ValidSha256, "key", null, 1, now);

	[Fact]
	public async Task RenameFileAsync_EmptyName_ReturnsInvalidFileName()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(Guid.NewGuid(), Guid.NewGuid(), "   ", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidFileName", result.Error!.Code);
		_ = harness.StoredFileRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RenameFileAsync_ReservedName_ReturnsInvalidFileName()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(Guid.NewGuid(), Guid.NewGuid(), ".", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidFileName", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_ControlCharacter_ReturnsInvalidFileName()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(Guid.NewGuid(), Guid.NewGuid(), "report.txt\r\nX-Injected: 1", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidFileName", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_BidiFormatCharacter_ReturnsInvalidFileName()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(Guid.NewGuid(), Guid.NewGuid(), "report‮gnp.exe", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidFileName", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_QuoteCharacter_ReturnsInvalidFileName()
	{
		// Непровалидированное имя попадает в заголовок Content-Disposition при скачивании
		// (SeaweedFsBlobStorage строит его сырой интерполяцией) - '"' ломает его синтаксис.
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(Guid.NewGuid(), Guid.NewGuid(), "report\"; evil=1.txt", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidFileName", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_NameExactly255Characters_Succeeds()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "old.txt", harness.TimeProvider.GetUtcNow());
		var name255 = new string('a', 255);
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RenameAsync(file.Id, ownerId, name255, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(ownerId, file.Id, name255, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(name255, result.Value.OriginalFileName);
	}

	[Fact]
	public async Task RenameFileAsync_NameOver255Characters_ReturnsFileNameTooLong()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();
		var name256 = new string('a', 256);

		var result = await sut.RenameFileAsync(Guid.NewGuid(), Guid.NewGuid(), name256, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.FileNameTooLong", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_UnknownFile_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.StoredFileRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((StoredFile?)null);
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(Guid.NewGuid(), Guid.NewGuid(), "new.txt", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_FileInTrash_ReturnsInTrash()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "old.txt", harness.TimeProvider.GetUtcNow())
			.SetDeletedAtUtc(harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(ownerId, file.Id, "new.txt", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InTrash", result.Error!.Code);
		_ = harness.StoredFileRepository.DidNotReceive().RenameAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RenameFileAsync_DeletedBetweenFetchAndWrite_ReturnsNotFound()
	{
		// Регрессия на найденную при проектировании гонку - см. аналогичный тест в
		// RenameFolderAsyncTests.
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "old.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RenameAsync(file.Id, ownerId, "new.txt", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(ownerId, file.Id, "new.txt", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_NameConflict_RawPostgresException_ReturnsConflict()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "old.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RenameAsync(file.Id, ownerId, "new.txt", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Throws(new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(ownerId, file.Id, "new.txt", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_NameConflict_WrappedDbUpdateException_ReturnsConflict()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "old.txt", harness.TimeProvider.GetUtcNow());
		var pgException = new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RenameAsync(file.Id, ownerId, "new.txt", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Throws(new DbUpdateException("duplicate key value", pgException));
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(ownerId, file.Id, "new.txt", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task RenameFileAsync_Valid_ReturnsRenamedSummaryWithUnchangedFields()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folderId = Guid.NewGuid();
		var file = CreateFile(ownerId, folderId, "old.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RenameAsync(file.Id, ownerId, "new.txt", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(ownerId, file.Id, "new.txt", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(file.Id, result.Value.Id);
		Assert.Equal(folderId, result.Value.FolderId);
		Assert.Equal("new.txt", result.Value.OriginalFileName);
		Assert.Equal(file.ContentType, result.Value.ContentType);
		Assert.Equal(file.SizeBytes, result.Value.SizeBytes);
		Assert.Equal(file.Status, result.Value.Status);
		Assert.Equal(file.CreatedAtUtc, result.Value.CreatedAtUtc);
		_ = harness.StoredFileRepository.Received(1).RenameAsync(
			file.Id, ownerId, "new.txt", harness.TimeProvider.GetUtcNow(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RenameFileAsync_TrimsWhitespace()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "old.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RenameAsync(file.Id, ownerId, "new.txt", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(ownerId, file.Id, "  new.txt  ", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("new.txt", result.Value.OriginalFileName);
	}

	[Fact]
	public async Task RenameFileAsync_SameNameAfterNormalization_ReturnsSuccessWithoutCallingRenameAsync()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.RenameFileAsync(ownerId, file.Id, "  report.txt  ", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("report.txt", result.Value.OriginalFileName);
		_ = harness.StoredFileRepository.DidNotReceive().RenameAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}
}
