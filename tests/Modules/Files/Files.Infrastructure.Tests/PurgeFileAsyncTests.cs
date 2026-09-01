using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class PurgeFileAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	private static StoredFile CreateTrashedFile(Guid ownerId, DateTimeOffset now) =>
		StoredFile.Create(Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10, ValidSha256, "storage-key", null, 1, now)
			.SetDeletedAtUtc(now);

	[Fact]
	public async Task PurgeFileAsync_UnknownFile_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.StoredFileRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((StoredFile?)null);
		var sut = harness.CreateSut();

		var result = await sut.PurgeFileAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task PurgeFileAsync_NotInTrash_ReturnsNotInTrashWithoutTouchingStorage()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
			Guid.NewGuid(), ownerId, null, "active.txt", "text/plain", 10, ValidSha256, "key", null, 1,
			harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.PurgeFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotInTrash", result.Error!.Code);
		_ = harness.StoredFileRepository.DidNotReceive().PurgeIfTrashedAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await harness.BlobStorage.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PurgeFileAsync_RaceLostAgainstAutomaticSweep_ReturnsNotFoundWithoutDeletingBlob()
	{
		// PurgeIfTrashedAsync возвращает false - файл уже окончательно удалён автоматической
		// очисткой в промежутке между fetch и этим вызовом. Блоб трогать нельзя - его, скорее
		// всего, уже удалила сама очистка (либо она вот-вот это сделает).
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateTrashedFile(ownerId, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository.PurgeIfTrashedAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.PurgeFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
		await harness.BlobStorage.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PurgeFileAsync_Valid_PurgesRowBeforeBlob()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateTrashedFile(ownerId, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository.PurgeIfTrashedAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.PurgeFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Received.InOrder(() =>
		{
			harness.StoredFileRepository.PurgeIfTrashedAsync(file.Id, ownerId, Arg.Any<CancellationToken>());
			harness.BlobStorage.DeleteObjectAsync(file.StorageKey, Arg.Any<CancellationToken>());
		});
	}
}
