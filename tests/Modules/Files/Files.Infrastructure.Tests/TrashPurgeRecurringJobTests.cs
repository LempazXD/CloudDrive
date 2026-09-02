using Files.Core.Application.Abstractions;
using Files.Infrastructure.BackgroundJobs;
using NSubstitute;
using Xunit;

namespace Files.Infrastructure.Tests;

public sealed class TrashPurgeRecurringJobTests
{
	[Fact]
	public async Task RunAsync_FirstBatchEmpty_CallsPurgeExpiredBatchAsyncOnce()
	{
		var purgeService = Substitute.For<ITrashPurgeService>();
		purgeService.PurgeExpiredBatchAsync(Arg.Any<CancellationToken>()).Returns(new TrashPurgeSummary(0, 0, 0));
		var sut = new TrashPurgeRecurringJob(purgeService);

		await sut.RunAsync(CancellationToken.None);

		await purgeService.Received(1).PurgeExpiredBatchAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RunAsync_NonEmptyThenEmptyBatch_StopsRightAfterFirstEmptyBatch()
	{
		var purgeService = Substitute.For<ITrashPurgeService>();
		purgeService.PurgeExpiredBatchAsync(Arg.Any<CancellationToken>()).Returns(
			new TrashPurgeSummary(200, 1000, 0),
			new TrashPurgeSummary(200, 1000, 0),
			new TrashPurgeSummary(0, 0, 0));
		var sut = new TrashPurgeRecurringJob(purgeService);

		await sut.RunAsync(CancellationToken.None);

		await purgeService.Received(3).PurgeExpiredBatchAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RunAsync_BacklogNeverEmpties_StopsAtIterationCap()
	{
		// Батчи всегда непустые - без потолка цикл крутился бы бесконечно, отжимая процесс от
		// следующего запланированного срабатывания Hangfire.
		var purgeService = Substitute.For<ITrashPurgeService>();
		purgeService.PurgeExpiredBatchAsync(Arg.Any<CancellationToken>()).Returns(new TrashPurgeSummary(200, 1000, 0));
		var sut = new TrashPurgeRecurringJob(purgeService);

		await sut.RunAsync(CancellationToken.None);

		await purgeService.Received(50).PurgeExpiredBatchAsync(Arg.Any<CancellationToken>());
	}
}
