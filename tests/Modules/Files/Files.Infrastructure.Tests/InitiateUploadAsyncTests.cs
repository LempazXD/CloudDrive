using Files.Core.Application.Abstractions;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class InitiateUploadAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	[Fact]
	public async Task InitiateUploadAsync_EmptyFileName_ReturnsInvalidFileName()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.InitiateUploadAsync(
			Guid.NewGuid(), null, "  ", "text/plain", 100, ValidSha256, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidFileName", result.Error!.Code);
	}

	[Fact]
	public async Task InitiateUploadAsync_NonPositiveSize_ReturnsInvalidSize()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.InitiateUploadAsync(
			Guid.NewGuid(), null, "report.txt", "text/plain", 0, ValidSha256, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidSize", result.Error!.Code);
	}

	[Fact]
	public async Task InitiateUploadAsync_InvalidSha256_ReturnsInvalidChecksum()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.InitiateUploadAsync(
			Guid.NewGuid(), null, "report.txt", "text/plain", 100, "not-a-hash", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidChecksum", result.Error!.Code);
	}

	[Fact]
	public async Task InitiateUploadAsync_UnknownFolder_ReturnsFolderNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var folderId = Guid.NewGuid();
		harness.FolderRepository.ExistsAsync(folderId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.InitiateUploadAsync(
			Guid.NewGuid(), folderId, "report.txt", "text/plain", 100, ValidSha256, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task InitiateUploadAsync_Valid_ReturnsUploadTargetAndPersistsFile()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var fileId = Guid.NewGuid();
		harness.GuidProvider.CreateVersion7().Returns(fileId);
		harness.BlobStorage
			.InitiateUploadAsync(Arg.Any<string>(), "text/plain", 100, Arg.Any<CancellationToken>())
			.Returns(new BlobUploadTarget(null, [new BlobUploadPart(1, "https://storage.local/presigned")]));
		var sut = harness.CreateSut();

		var result = await sut.InitiateUploadAsync(
			ownerId, null, "report.txt", "text/plain", 100, ValidSha256, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(fileId, result.Value.FileId);
		Assert.Null(result.Value.UploadId);
		Assert.Single(result.Value.Parts);
		_ = harness.BlobStorage.Received(1)
			.InitiateUploadAsync($"{ownerId}/{fileId}", "text/plain", 100, Arg.Any<CancellationToken>());
		_ = harness.StoredFileRepository.Received(1).AddAsync(
			Arg.Is<Core.Domain.StoredFile>(f => f != null && f.Id == fileId && f.StorageKey == $"{ownerId}/{fileId}"),
			Arg.Any<CancellationToken>());
		_ = harness.StoredFileRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
	}
}
