using Amazon.S3;
using Files.Core.Application.Abstractions;
using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared.Kernel.Results;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class CompleteUploadAsyncTests
{
	private static readonly string ValidSha256 = new('a', 64);

	[Fact]
	public async Task CompleteUploadAsync_UnknownFile_ReturnsNotFound()
	{
		var harness = new FilesServiceTestHarness();
		harness.StoredFileRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns((StoredFile?)null);
		var sut = harness.CreateSut();

		var result = await sut.CompleteUploadAsync(Guid.NewGuid(), Guid.NewGuid(), [], CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.NotFound", result.Error!.Code);
	}

	[Fact]
	public async Task CompleteUploadAsync_PartCountMismatch_ReturnsChecksumMismatch()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
			Guid.NewGuid(), ownerId, null, "big.bin", "application/octet-stream", 100_000_000,
			ValidSha256, "key", "upload-1", expectedPartCount: 3, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		var sut = harness.CreateSut();

		var result = await sut.CompleteUploadAsync(ownerId, file.Id, [new BlobUploadedPart(1, "etag-1")], CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.ChecksumMismatch", result.Error!.Code);
		_ = harness.StoredFileRepository.DidNotReceive().TryCompleteAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task CompleteUploadAsync_RaceLost_ReturnsAlreadyCompleted()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
			Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10,
			ValidSha256, "key", null, expectedPartCount: 1, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository.TryCompleteAsync(
				file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.CompleteUploadAsync(ownerId, file.Id, [], CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.AlreadyCompleted", result.Error!.Code);
		await harness.BlobStorage.DidNotReceive().CompleteUploadAsync(
			Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<BlobUploadedPart>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task CompleteUploadAsync_StorageRejectsCompletion_MarksFailedAndReturnsCompletionFailed()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
			Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10,
			ValidSha256, "key", null, expectedPartCount: 1, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository.TryCompleteAsync(
				file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
			.Returns(true);
		harness.BlobStorage.CompleteUploadAsync(
				file.StorageKey, file.UploadId, Arg.Any<IReadOnlyList<BlobUploadedPart>>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new AmazonS3Exception("Object was never actually uploaded."));
		var sut = harness.CreateSut();

		var result = await sut.CompleteUploadAsync(ownerId, file.Id, [], CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Files.File.CompletionFailed", result.Error!.Code);
		_ = harness.StoredFileRepository.Received(1).MarkFailedAsync(file.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
		_ = harness.StoredFileRepository.DidNotReceive().MarkCompletedAsync(
			Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task CompleteUploadAsync_Valid_MarksCompletedAndReturnsSummary()
	{
		var harness = new FilesServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = StoredFile.Create(
			Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", 10,
			ValidSha256, "key", null, expectedPartCount: 1, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository.GetByIdAsync(file.Id, ownerId, Arg.Any<CancellationToken>()).Returns(file);
		harness.StoredFileRepository.TryCompleteAsync(
				file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
			.Returns(true);
		harness.BlobStorage.CompleteUploadAsync(
				file.StorageKey, file.UploadId, Arg.Any<IReadOnlyList<BlobUploadedPart>>(), Arg.Any<CancellationToken>())
			.Returns(new BlobObjectInfo("etag-abc", 10));
		var sut = harness.CreateSut();

		var result = await sut.CompleteUploadAsync(ownerId, file.Id, [], CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(FileStatus.Completed, result.Value.Status);
		_ = harness.StoredFileRepository.Received(1).MarkCompletedAsync(file.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}
}
