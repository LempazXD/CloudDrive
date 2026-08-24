using System.Diagnostics.CodeAnalysis;
using Files.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Files.Infrastructure.IntegrationTests;

/// <summary>
/// Один эфемерный контейнер Postgres на весь набор тестов в коллекции <see cref="PostgresCollection"/>
/// (не на каждый тест - поднятие контейнера не бесплатно). Тесты изолируются друг от друга не
/// пересозданием БД, а тем, что каждый тест работает со своими собственными Guid.NewGuid()
/// владельцами/папками - тот же принцип, что и у остальной части набора тестов проекта.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17").Build();

	public string ConnectionString => container.GetConnectionString();

	public async Task InitializeAsync()
	{
		await container.StartAsync();

		var options = new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(ConnectionString).Options;
		await using var db = new FilesDbContext(options);
		await db.Database.MigrateAsync();
	}

	public async Task DisposeAsync() => await container.DisposeAsync();
}

// CA1711: xUnit's [CollectionDefinition] marker-class idiom requires this shape - the type isn't
// an actual collection, just a name xUnit groups test classes by via [Collection(nameof(...))].
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification =
	"xUnit collection-definition marker class, not an actual collection type.")]
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
