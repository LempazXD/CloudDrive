using Files.Core.Domain;
using Files.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Files.Infrastructure.IntegrationTests;

/// <summary>
/// Проверяет корзину против настоящего Postgres - то, что моки в StoredFileRepository не могут
/// доказать: что мягко удалённый файл действительно освобождает своё место в partial unique
/// индексе (StoredFileConfiguration) для новой загрузки с тем же именем, и что условные
/// удаления (PurgeIfTrashedAsync/PurgeIfStillExpiredAsync) реально не затрагивают строку, когда
/// их условие не выполняется - при мокинге это просто заранее заданный bool, здесь - реальный
/// WHERE, выполненный Postgres.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class StoredFileRepositoryTrashTests(PostgresFixture fixture)
{
	private static readonly string ValidSha256 = new('a', 64);

	private FilesDbContext CreateContext() =>
		new(new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(fixture.ConnectionString).Options);

	private static StoredFile CreateFile(Guid ownerId, string name, DateTimeOffset now) =>
		StoredFile.Create(Guid.NewGuid(), ownerId, null, name, "text/plain", 10, ValidSha256, $"key-{Guid.NewGuid()}", null, 1, now);

	private async Task SeedAsync(params StoredFile[] files)
	{
		await using var db = CreateContext();
		db.StoredFiles.AddRange(files);
		await db.SaveChangesAsync();
	}

	[Fact]
	public async Task ListAsync_ExcludesTrashedFiles()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var active = CreateFile(ownerId, "active.txt", now);
		var trashed = CreateFile(ownerId, "trashed.txt", now);
		await SeedAsync(active, trashed);

		await using var db = CreateContext();
		var sut = new StoredFileRepository(db);
		Assert.True(await sut.SoftDeleteAsync(trashed.Id, ownerId, now, CancellationToken.None));

		var listed = await sut.ListAsync(ownerId, null, null, 10, CancellationToken.None);

		Assert.Single(listed);
		Assert.Equal(active.Id, listed[0].Id);
	}

	[Fact]
	public async Task ListTrashAsync_ReturnsOnlyTrashedFiles()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var active = CreateFile(ownerId, "active.txt", now);
		var trashed = CreateFile(ownerId, "trashed.txt", now);
		await SeedAsync(active, trashed);

		await using var db = CreateContext();
		var sut = new StoredFileRepository(db);
		Assert.True(await sut.SoftDeleteAsync(trashed.Id, ownerId, now, CancellationToken.None));

		var listed = await sut.ListTrashAsync(ownerId, null, 10, CancellationToken.None);

		Assert.Single(listed);
		Assert.Equal(trashed.Id, listed[0].Id);
	}

	[Fact]
	public async Task SoftDeleteAsync_FreesNameForNewUploadInSameFolder()
	{
		// Ядро всей корзины: partial unique индекс на (OwnerId, FolderId, Name) должен исключать
		// DeletedAtUtc IS NOT NULL, иначе перенос в корзину не освобождает имя, и вторая
		// загрузка с тем же именем упадёт в NameConflict, хотя пользователь уже не видит
		// исходный файл в обычном листинге.
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var original = CreateFile(ownerId, "report.pdf", now);
		await SeedAsync(original);

		await using var db = CreateContext();
		var sut = new StoredFileRepository(db);
		Assert.True(await sut.SoftDeleteAsync(original.Id, ownerId, now, CancellationToken.None));

		var replacement = CreateFile(ownerId, "report.pdf", now);
		await sut.AddAsync(replacement, CancellationToken.None);
		await sut.SaveChangesAsync(CancellationToken.None); // не должно бросить unique violation

		await using var verifyDb = CreateContext();
		var activeCount = await verifyDb.StoredFiles.AsNoTracking()
			.CountAsync(f => f.OwnerId == ownerId && f.OriginalFileName == "report.pdf" && f.DeletedAtUtc == null);
		Assert.Equal(1, activeCount);
	}

	[Fact]
	public async Task RestoreAsync_MakesFileActiveAgain()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var file = CreateFile(ownerId, "restored.txt", now);
		await SeedAsync(file);

		await using var db = CreateContext();
		var sut = new StoredFileRepository(db);
		Assert.True(await sut.SoftDeleteAsync(file.Id, ownerId, now, CancellationToken.None));
		Assert.True(await sut.RestoreAsync(file.Id, ownerId, now, CancellationToken.None));

		var active = await sut.ListAsync(ownerId, null, null, 10, CancellationToken.None);
		var trash = await sut.ListTrashAsync(ownerId, null, 10, CancellationToken.None);

		Assert.Single(active);
		Assert.Empty(trash);
	}

	[Fact]
	public async Task PurgeIfStillExpiredAsync_NotYetExpired_LeavesRowUntouched()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var file = CreateFile(ownerId, "recent.txt", now);
		await SeedAsync(file);

		await using var db = CreateContext();
		var sut = new StoredFileRepository(db);
		Assert.True(await sut.SoftDeleteAsync(file.Id, ownerId, now, CancellationToken.None));

		// cutoff в прошлом относительно момента удаления - ещё не просрочен.
		var purged = await sut.PurgeIfStillExpiredAsync(file.Id, ownerId, now.AddDays(-30), CancellationToken.None);

		Assert.False(purged);
		await using var verifyDb = CreateContext();
		Assert.NotNull(await verifyDb.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == file.Id));
	}

	[Fact]
	public async Task PurgeIfStillExpiredAsync_Expired_DeletesRow()
	{
		var ownerId = Guid.NewGuid();
		var deletedAt = DateTimeOffset.UtcNow.AddDays(-31); // трэшнут 31 день назад
		var file = CreateFile(ownerId, "old.txt", DateTimeOffset.UtcNow.AddDays(-31));
		await SeedAsync(file);

		await using var db = CreateContext();
		var sut = new StoredFileRepository(db);
		Assert.True(await sut.SoftDeleteAsync(file.Id, ownerId, deletedAt, CancellationToken.None));

		// cutoff = сейчас минус 30 дней - файл, удалённый 31 день назад, просрочен.
		var purged = await sut.PurgeIfStillExpiredAsync(
			file.Id, ownerId, DateTimeOffset.UtcNow.AddDays(-30), CancellationToken.None);

		Assert.True(purged);
		await using var verifyDb = CreateContext();
		Assert.Null(await verifyDb.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == file.Id));
	}

	[Fact]
	public async Task PurgeIfTrashedAsync_ActiveFile_LeavesRowUntouched()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var file = CreateFile(ownerId, "active.txt", now);
		await SeedAsync(file);

		await using var db = CreateContext();
		var sut = new StoredFileRepository(db);

		var purged = await sut.PurgeIfTrashedAsync(file.Id, ownerId, CancellationToken.None);

		Assert.False(purged);
		await using var verifyDb = CreateContext();
		Assert.NotNull(await verifyDb.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == file.Id));
	}
}
