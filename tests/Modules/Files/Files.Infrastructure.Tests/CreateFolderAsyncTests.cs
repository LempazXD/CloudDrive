using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class CreateFolderAsyncTests
{
	[Fact]
	public async Task CreateFolderAsync_EmptyName_ReturnsInvalidName()
	{
		var harness = new FilesServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.CreateFolderAsync(Guid.NewGuid(), null, "   ", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.InvalidName", result.Error!.Code);
	}

	[Fact]
	public async Task CreateFolderAsync_UnknownParent_ReturnsFolderNotFound()
	{
		var harness = new FilesServiceTestHarness();
		var parentId = Guid.NewGuid();
		harness.FolderRepository.ExistsAsync(parentId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.CreateFolderAsync(Guid.NewGuid(), parentId, "Photos", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task CreateFolderAsync_Valid_ReturnsFolderSummary()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folderId = Guid.NewGuid();
		harness.GuidProvider.CreateVersion7().Returns(folderId);
		var sut = harness.CreateSut();

		var result = await sut.CreateFolderAsync(ownerId, null, "Photos", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(folderId, result.Value.Id);
		Assert.Equal("Photos", result.Value.Name);
		_ = harness.FolderRepository.Received(1).AddAsync(Arg.Any<Core.Domain.Folder>(), Arg.Any<CancellationToken>());
		_ = harness.FolderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
	}
}
