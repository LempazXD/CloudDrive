using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class DeleteFileAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

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
	public async Task DeleteFileAsync_Valid_DeletesBlobBeforeRow()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
			Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10, ValidSha256, "storage-key", null, 1,
			harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository.DeleteAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFileAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Received.InOrder(() =>
		{
			harness.BlobStorage.DeleteObjectAsync(file.StorageKey, Arg.Any<CancellationToken>());
			harness.StoredFileRepository.DeleteAsync(file.Id, ownerId, Arg.Any<CancellationToken>());
		});
	}
}
