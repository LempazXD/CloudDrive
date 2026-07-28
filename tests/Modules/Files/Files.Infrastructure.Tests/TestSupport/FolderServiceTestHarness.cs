using Files.Core.Application.Abstractions;
using Files.Infrastructure.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shared.Kernel.Guids;

namespace Files.Infrastructure.Tests.TestSupport;

/// <summary>
/// Собирает все зависимости <see cref="FolderService"/> как NSubstitute-моки (кроме
/// <see cref="TimeProvider"/> - его проще использовать настоящим) и строит сам SUT. Каждый тест
/// создаёт свой экземпляр, поэтому моки не расшарены между тестами.
/// </summary>
internal sealed class FolderServiceTestHarness
{
	public IFolderRepository FolderRepository { get; } = Substitute.For<IFolderRepository>();
	public IStoredFileRepository StoredFileRepository { get; } = Substitute.For<IStoredFileRepository>();
	public IGuidProvider GuidProvider { get; } = Substitute.For<IGuidProvider>();
	public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	public ILogger<FolderService> Logger { get; } = Substitute.For<ILogger<FolderService>>();

	public IFolderService CreateSut() => new FolderService(
		FolderRepository,
		StoredFileRepository,
		GuidProvider,
		TimeProvider,
		Logger);
}
