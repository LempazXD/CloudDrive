using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class DeleteFolderAsyncTests
{
	[Fact]
	public async Task DeleteFolderAsync_UnknownFolder_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.FolderRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((Folder?)null);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFolderAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task DeleteFolderAsync_HasSubfolders_ReturnsNotEmpty()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.HasSubfoldersAsync(folder.Id, Arg.Any<CancellationToken>()).Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFolderAsync(ownerId, folder.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotEmpty", result.Error!.Code);
		_ = harness.FolderRepository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task DeleteFolderAsync_HasFiles_ReturnsNotEmpty()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.HasSubfoldersAsync(folder.Id, Arg.Any<CancellationToken>()).Returns(false);
		harness.StoredFileRepository.ExistsInFolderAsync(folder.Id, Arg.Any<CancellationToken>()).Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFolderAsync(ownerId, folder.Id, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotEmpty", result.Error!.Code);
	}

	[Fact]
	public async Task DeleteFolderAsync_Empty_DeletesFolder()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		harness.FolderRepository.HasSubfoldersAsync(folder.Id, Arg.Any<CancellationToken>()).Returns(false);
		harness.StoredFileRepository.ExistsInFolderAsync(folder.Id, Arg.Any<CancellationToken>()).Returns(false);
		harness.FolderRepository.DeleteAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.DeleteFolderAsync(ownerId, folder.Id, CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.FolderRepository.Received(1).DeleteAsync(folder.Id, ownerId, Arg.Any<CancellationToken>());
	}
}
