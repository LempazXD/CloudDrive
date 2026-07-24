using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class GetFolderAsyncTests
{
	[Fact]
	public async Task GetFolderAsync_UnknownFolder_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.FolderRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((Folder?)null);
		var sut = harness.CreateSut();

		var result = await sut.GetFolderAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.Folder.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task GetFolderAsync_Found_ReturnsFolderSummary()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var folder = Folder.Create(Guid.NewGuid(), ownerId, null, "Photos", harness.TimeProvider.GetUtcNow());
		harness.FolderRepository.GetByIdAsync(folder.Id, ownerId, Arg.Any<CancellationToken>()).Returns(folder);
		var sut = harness.CreateSut();

		var result = await sut.GetFolderAsync(ownerId, folder.Id, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(folder.Id, result.Value.Id);
		Assert.Equal("Photos", result.Value.Name);
	}
}
