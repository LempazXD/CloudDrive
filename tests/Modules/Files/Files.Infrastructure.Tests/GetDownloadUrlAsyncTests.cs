using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class GetDownloadUrlAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	[Fact]
	public async Task GetDownloadUrlAsync_UnknownFile_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.StoredFileRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((StoredFile?)null);
		var sut = harness.CreateSut();

		var result = await sut.GetDownloadUrlAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task GetDownloadUrlAsync_NotCompleted_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
			Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10, ValidSha256, "key", null, 1, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.GetDownloadUrlAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
		await harness.BlobStorage.DidNotReceive().GetPresignedDownloadUrlAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task GetDownloadUrlAsync_Completed_ReturnsPresignedUrl()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
				Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10, ValidSha256, "key", null, 1,
				harness.TimeProvider.GetUtcNow())
			.SetStatus(FileStatus.Completed);
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.BlobStorage
			.GetPresignedDownloadUrlAsync(file.StorageKey, file.OriginalFileName, file.ContentType, Arg.Any<CancellationToken>())
			.Returns("https://storage.local/presigned-download");
		var sut = harness.CreateSut();

		var result = await sut.GetDownloadUrlAsync(ownerId, file.Id, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("https://storage.local/presigned-download", result.Value);
	}
}
