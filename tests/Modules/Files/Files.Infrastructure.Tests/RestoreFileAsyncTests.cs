using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Npgsql;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class RestoreFileAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	private static StoredFile CreateTrashedFile(Guid ownerId, Guid? folderId, string name, DateTimeOffset now) =>
		StoredFile.Create(Guid.NewGuid(), ownerId, folderId, name, "text/plain", 100, ValidSha256, "key", null, 1, now)
			.SetDeletedAtUtc(now);

	[Fact]
	public async Task RestoreFileAsync_UnknownFile_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.StoredFileRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((StoredFile?)null);
		var sut = harness.CreateSut();

		var result = await sut.RestoreFileAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task RestoreFileAsync_NotInTrash_ReturnsNotInTrash()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
			Guid.NewGuid(), ownerId, null, "active.txt", "text/plain", 100, ValidSha256, "key", null, 1,
			harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.RestoreFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotInTrash", result.Error!.Code);
		_ = harness.StoredFileRepository.DidNotReceive().RestoreAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RestoreFileAsync_PurgedBetweenFetchAndWrite_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateTrashedFile(ownerId, null, "gone.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RestoreAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.RestoreFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task RestoreFileAsync_NameConflict_RawPostgresException_ReturnsConflict()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateTrashedFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RestoreAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Throws(new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));
		var sut = harness.CreateSut();

		var result = await sut.RestoreFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task RestoreFileAsync_NameConflict_WrappedDbUpdateException_ReturnsConflict()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateTrashedFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		var pgException = new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RestoreAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Throws(new DbUpdateException("duplicate key value", pgException));
		var sut = harness.CreateSut();

		var result = await sut.RestoreFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NameConflict", result.Error!.Code);
	}

	[Fact]
	public async Task RestoreFileAsync_Valid_ReturnsSummaryWithClearedDeletedAtUtc()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateTrashedFile(ownerId, null, "report.txt", harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.RestoreAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.RestoreFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(file.Id, result.Value.Id);
		Assert.Equal(file.OriginalFileName, result.Value.OriginalFileName);
		Assert.Null(result.Value.DeletedAtUtc);
		Assert.Null(result.Value.PurgeAtUtc);
		_ = harness.StoredFileRepository.Received(1).RestoreAsync(
			file.Id, ownerId, harness.TimeProvider.GetUtcNow(), Arg.Any<CancellationToken>());
	}
}
