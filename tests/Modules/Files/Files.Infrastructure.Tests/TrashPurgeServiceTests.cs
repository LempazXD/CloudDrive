using Files.Core.Domain;
using Files.Infrastructure.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class TrashPurgeServiceTests
{
	private static readonly string ValidSha256 = new('a', 64);

	private static StoredFile CreateFile(Guid ownerId, long sizeBytes, DateTimeOffset now) =>
		StoredFile.Create(Guid.NewGuid(), ownerId, null, "a.txt", "text/plain", sizeBytes, ValidSha256, "key", null, 1, now);

	[Fact]
	public async Task PurgeExpiredBatchAsync_NoExpiredFiles_ReturnsEmptySummary()
	{
		var harness = new TrashPurgeServiceTestHarness();
		harness.StoredFileRepository
			.ListExpiredTrashAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([]);
		var sut = harness.CreateSut();

		var summary = await sut.PurgeExpiredBatchAsync(CancellationToken.None);

		Assert.Equal(0, summary.PurgedCount);
		Assert.Equal(0, summary.ReclaimedBytes);
		Assert.Equal(0, summary.FailedCount);
		await harness.BlobStorage.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PurgeExpiredBatchAsync_ExpiredFile_PurgesRowBeforeBlob()
	{
		var harness = new TrashPurgeServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, 1000, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository
			.ListExpiredTrashAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([file]);
		harness.StoredFileRepository
			.PurgeIfStillExpiredAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var summary = await sut.PurgeExpiredBatchAsync(CancellationToken.None);

		Assert.Equal(1, summary.PurgedCount);
		Assert.Equal(1000, summary.ReclaimedBytes);
		Assert.Equal(0, summary.FailedCount);
		Received.InOrder(() =>
		{
			harness.StoredFileRepository.PurgeIfStillExpiredAsync(
				file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
			harness.BlobStorage.DeleteObjectAsync(file.StorageKey, Arg.Any<CancellationToken>());
		});
	}

	[Fact]
	public async Task PurgeExpiredBatchAsync_RaceLostAgainstRestore_SkipsBlobDeletion()
	{
		// PurgeIfStillExpiredAsync возвращает false - файл восстановили (или восстановили и
		// удалили заново со свежим DeletedAtUtc) в промежутке между ListExpiredTrashAsync и этим
		// вызовом. Один и тот же стаб покрывает оба случая - сервису неизвестно, какой именно.
		var harness = new TrashPurgeServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, 1000, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository
			.ListExpiredTrashAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([file]);
		harness.StoredFileRepository
			.PurgeIfStillExpiredAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var sut = harness.CreateSut();

		var summary = await sut.PurgeExpiredBatchAsync(CancellationToken.None);

		Assert.Equal(0, summary.PurgedCount);
		Assert.Equal(0, summary.ReclaimedBytes);
		Assert.Equal(0, summary.FailedCount);
		await harness.BlobStorage.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PurgeExpiredBatchAsync_DbPurgeThrows_CountsAsFailedAndContinuesWithNextFile()
	{
		var harness = new TrashPurgeServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var failing = CreateFile(ownerId, 1000, harness.TimeProvider.GetUtcNow());
		var succeeding = CreateFile(ownerId, 500, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository
			.ListExpiredTrashAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([failing, succeeding]);
		harness.StoredFileRepository
			.PurgeIfStillExpiredAsync(failing.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("transient DB error"));
		harness.StoredFileRepository
			.PurgeIfStillExpiredAsync(succeeding.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var summary = await sut.PurgeExpiredBatchAsync(CancellationToken.None);

		Assert.Equal(1, summary.PurgedCount);
		Assert.Equal(500, summary.ReclaimedBytes);
		Assert.Equal(1, summary.FailedCount);
	}

	[Fact]
	public async Task PurgeExpiredBatchAsync_BlobDeleteThrows_StillCountsRowAsPurged()
	{
		// Строка уже удалена условным удалением - это не "не удалось запурджить", а орфан
		// объекта в хранилище. Считается в PurgedCount/ReclaimedBytes, не в FailedCount -
		// ретраить нечего, строки уже нет.
		var harness = new TrashPurgeServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file = CreateFile(ownerId, 1000, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository
			.ListExpiredTrashAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([file]);
		harness.StoredFileRepository
			.PurgeIfStillExpiredAsync(file.Id, ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		harness.BlobStorage
			.DeleteObjectAsync(file.StorageKey, Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("storage unavailable"));
		var sut = harness.CreateSut();

		var summary = await sut.PurgeExpiredBatchAsync(CancellationToken.None);

		Assert.Equal(1, summary.PurgedCount);
		Assert.Equal(1000, summary.ReclaimedBytes);
		Assert.Equal(0, summary.FailedCount);
	}

	[Fact]
	public async Task PurgeExpiredBatchAsync_MultipleExpiredFiles_AggregatesReclaimedBytes()
	{
		var harness = new TrashPurgeServiceTestHarness();
		var ownerId = Guid.NewGuid();
		var file1 = CreateFile(ownerId, 1000, harness.TimeProvider.GetUtcNow());
		var file2 = CreateFile(ownerId, 2500, harness.TimeProvider.GetUtcNow());
		harness.StoredFileRepository
			.ListExpiredTrashAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([file1, file2]);
		harness.StoredFileRepository
			.PurgeIfStillExpiredAsync(Arg.Any<Guid>(), ownerId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var summary = await sut.PurgeExpiredBatchAsync(CancellationToken.None);

		Assert.Equal(2, summary.PurgedCount);
		Assert.Equal(3500, summary.ReclaimedBytes);
	}
}
