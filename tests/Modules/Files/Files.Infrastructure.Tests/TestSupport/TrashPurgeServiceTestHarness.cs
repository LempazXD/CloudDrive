using Files.Core.Application.Abstractions;
using Files.Infrastructure.Application;
using Files.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Files.Infrastructure.Tests.TestSupport;

/// <summary>
/// Собирает все зависимости <see cref="TrashPurgeService"/> как NSubstitute-моки (кроме
/// <see cref="TimeProvider"/> и <see cref="TrashOptions"/> - их проще использовать настоящими) и
/// строит сам SUT. Тот же принцип, что <see cref="FilesServiceTestHarness"/>.
/// </summary>
internal sealed class TrashPurgeServiceTestHarness
{
	public IStoredFileRepository StoredFileRepository { get; } = Substitute.For<IStoredFileRepository>();
	public IBlobStorage BlobStorage { get; } = Substitute.For<IBlobStorage>();
	public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	public ILogger<TrashPurgeService> Logger { get; } = Substitute.For<ILogger<TrashPurgeService>>();

	public TrashOptions TrashOptions { get; } = new()
	{
		RetentionPeriod = TimeSpan.FromDays(30),
		PurgeBatchSize = 200
	};

	public ITrashPurgeService CreateSut() => new TrashPurgeService(
		StoredFileRepository,
		BlobStorage,
		TimeProvider,
		Options.Create(TrashOptions),
		Logger);
}
