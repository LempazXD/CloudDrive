using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class ListFilesAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	[Fact]
	public async Task ListFilesAsync_NonPositiveLimit_ReturnsInvalidPageSize()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ListFilesAsync(Guid.NewGuid(), null, null, 0, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidPageSize", result.Error!.Code);
	}

	[Fact]
	public async Task ListFilesAsync_InvalidCursor_ReturnsInvalidCursor()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ListFilesAsync(Guid.NewGuid(), null, "not-base64-guid!!", 20, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.InvalidCursor", result.Error!.Code);
	}

	[Fact]
	public async Task ListFilesAsync_UnknownFolder_ReturnsFolderNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var folderId = Guid.NewGuid();
		harness.FolderRepository.ExistsAsync(folderId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.ListFilesAsync(Guid.NewGuid(), folderId, null, 20, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task ListFilesAsync_MoreItemsThanLimit_ReturnsNextCursor()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var now = harness.TimeProvider.GetUtcNow();
		var file1 = StoredFile.Create(Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10, ValidSha256, "key1", null, 1, now);
		var file2 = StoredFile.Create(Guid.NewGuid(), ownerId, null, "b.txt", "text/plain", 10, ValidSha256, "key2", null, 1, now);
		harness.StoredFileRepository.ListAsync(ownerId, null, null, 2, Arg.Any<CancellationToken>())
			.Returns([file1, file2]);
		var sut = harness.CreateSut();

		var result = await sut.ListFilesAsync(ownerId, null, null, 1, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value.Items);
		Assert.Equal(file1.Id, result.Value.Items[0].Id);
		Assert.NotNull(result.Value.NextCursor);
	}
}
