using Files.Core.Application.Abstractions;
using Files.Infrastructure.Application;
using Files.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shared.Kernel.Guids;

namespace Files.Infrastructure.Tests.TestSupport;

/// <summary>
/// Собирает все зависимости <see cref="FilesService"/> как NSubstitute-моки (кроме
/// <see cref="TimeProvider"/> и <see cref="ObjectStorageOptions"/> - их проще использовать
/// настоящими) и строит сам SUT. Каждый тест создаёт свой экземпляр, поэтому моки не расшарены
/// между тестами.
/// </summary>
internal sealed class FilesServiceTestHarness
{
	public IStoredFileRepository StoredFileRepository { get; } = Substitute.For<IStoredFileRepository>();
	public IFolderRepository FolderRepository { get; } = Substitute.For<IFolderRepository>();
	public IBlobStorage BlobStorage { get; } = Substitute.For<IBlobStorage>();
	public IGuidProvider GuidProvider { get; } = Substitute.For<IGuidProvider>();
	public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	public ILogger<FilesService> Logger { get; } = Substitute.For<ILogger<FilesService>>();

	public ObjectStorageOptions ObjectStorageOptions { get; } = new()
	{
		Endpoint = "http://localhost:8333",
		AccessKey = "test-access-key",
		SecretKey = "test-secret-key",
		Bucket = "test-bucket",
		CompletionStaleAfter = TimeSpan.FromMinutes(2)
	};

	public IFilesService CreateSut() => new FilesService(
		StoredFileRepository,
		FolderRepository,
		BlobStorage,
		GuidProvider,
		TimeProvider,
		Options.Create(ObjectStorageOptions),
		Logger);
}
