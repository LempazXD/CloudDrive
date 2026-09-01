using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class DeleteFileAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	private static StoredFile CreateFile(Guid ownerId, DateTimeOffset now) =>
		StoredFile.Create(Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10, ValidSha256, "storage-key", null, 1, now);

	[Fact]
	public async Task DeleteFileAsync_UnknownFile_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.StoredFileRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((StoredFile?)null);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFileAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task DeleteFileAsync_Valid_SoftDeletesWithoutTouchingBlobStorage()
	{
		// Мягкое удаление - строка помечается корзиной, объект в хранилище не трогается до purge.
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.SoftDeleteAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.StoredFileRepository.Received(1).SoftDeleteAsync(
			file.Id, ownerId, harness.TimeProvider.GetUtcNow(), Arg.Any<CancellationToken>());
		await harness.BlobStorage.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task DeleteFileAsync_AlreadyInTrash_ReturnsSuccessWithoutCallingSoftDeleteAsync()
	{
		// Повторный DELETE на уже трэшнутый файл - идемпотентный no-op, не эскалирует до purge.
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, harness.TimeProvider.GetUtcNow()).SetDeletedAtUtc(harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.StoredFileRepository.DidNotReceive().SoftDeleteAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task DeleteFileAsync_DeletedBetweenFetchAndWrite_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository
			.SoftDeleteAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}
}
