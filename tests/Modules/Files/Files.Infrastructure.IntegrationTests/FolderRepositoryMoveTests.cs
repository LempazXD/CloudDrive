using Files.Core.Application.Abstractions;
using Files.Core.Domain;
using Files.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Files.Infrastructure.IntegrationTests;

/// <summary>
/// Проверяет FolderRepository.MoveAsync против настоящего Postgres (Testcontainers) - то, что
/// юнит-тесты на моках (MoveFolderAsyncTests в Files.Infrastructure.Tests) в принципе не могут
/// проверить: сам рекурсивный CTE (обнаружение цикла) и реальную сериализацию через
/// pg_advisory_xact_lock при параллельных вызовах. См. ADR 0016 и план в
/// src/Modules/CLAUDE.md ("Move").
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FolderRepositoryMoveTests(PostgresFixture fixture)
{
	private FilesDbContext CreateContext() =>
		new(new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(fixture.ConnectionString).Options);

	private static List<Folder> BuildChain(Guid ownerId, int depth, DateTimeOffset now)
	{
		var chain = new List<Folder>();
		Guid? parentId = null;
		for (var i = 0; i < depth; i++)
		{
			var folder = Folder.Create(Guid.NewGuid(), ownerId, parentId, $"Level{i}", now);
			chain.Add(folder);
			parentId = folder.Id;
		}

		return chain;
	}

	private async Task SeedAsync(params Folder[] folders)
	{
		await using var db = CreateContext();
		db.Folders.AddRange(folders);
		await db.SaveChangesAsync();
	}

	[Fact]
	public async Task MoveAsync_NoCycle_MovesSuccessfully()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var a = Folder.Create(Guid.NewGuid(), ownerId, null, "A", now);
		var b = Folder.Create(Guid.NewGuid(), ownerId, null, "B", now);
		await SeedAsync(a, b);

		await using var db = CreateContext();
		var sut = new FolderRepository(db);

		var outcome = await sut.MoveAsync(a.Id, ownerId, b.Id, CancellationToken.None);

		Assert.Equal(FolderMoveOutcome.Moved, outcome);

		await using var verifyDb = CreateContext();
		var moved = await verifyDb.Folders.AsNoTracking().SingleAsync(f => f.Id == a.Id);
		Assert.Equal(b.Id, moved.ParentFolderId);
	}

	[Fact]
	public async Task MoveAsync_DirectCycle_ReturnsWouldCreateCycle()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var a = Folder.Create(Guid.NewGuid(), ownerId, null, "A", now);
		var c = Folder.Create(Guid.NewGuid(), ownerId, a.Id, "C", now); // C - прямой потомок A
		await SeedAsync(a, c);

		await using var db = CreateContext();
		var sut = new FolderRepository(db);

		// Пытаемся переместить A под её же прямого потомка C.
		var outcome = await sut.MoveAsync(a.Id, ownerId, c.Id, CancellationToken.None);

		Assert.Equal(FolderMoveOutcome.WouldCreateCycle, outcome);

		await using var verifyDb = CreateContext();
		var unchanged = await verifyDb.Folders.AsNoTracking().SingleAsync(f => f.Id == a.Id);
		Assert.Null(unchanged.ParentFolderId);
	}

	[Fact]
	public async Task MoveAsync_DeepCycle_ReturnsWouldCreateCycle()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var chain = BuildChain(ownerId, 4, now); // chain[0] (root) -> chain[1] -> chain[2] -> chain[3]
		await SeedAsync([.. chain]);

		await using var db = CreateContext();
		var sut = new FolderRepository(db);

		// Перемещаем корень цепочки под её самого глубокого потомка - CTE должен подняться
		// на несколько уровней, а не только на один, чтобы это поймать.
		var outcome = await sut.MoveAsync(chain[0].Id, ownerId, chain[^1].Id, CancellationToken.None);

		Assert.Equal(FolderMoveOutcome.WouldCreateCycle, outcome);
	}

	[Fact]
	public async Task MoveAsync_LegitimateDeepChain_DoesNotFalsePositive()
	{
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var chain = BuildChain(ownerId, 30, now);
		var unrelated = Folder.Create(Guid.NewGuid(), ownerId, null, "Unrelated", now);
		await SeedAsync([.. chain, unrelated]);

		await using var db = CreateContext();
		var sut = new FolderRepository(db);

		// unrelated нигде не входит в цепочку из 30 уровней - перемещение под самый глубокий
		// узел должно пройти, доказывая, что обход поднимается по всей цепочке до конца
		// (до NULL-корня), а не ложно останавливается раньше.
		var outcome = await sut.MoveAsync(unrelated.Id, ownerId, chain[^1].Id, CancellationToken.None);

		Assert.Equal(FolderMoveOutcome.Moved, outcome);
	}

	[Fact]
	public async Task MoveAsync_ConcurrentCrossSubtreeMoves_SerializeCorrectly()
	{
		// Write skew сценарий из ADR 0016: A -> C изначально, B - несвязанная корневая папка.
		// По отдельности оба перемещения (A под B, и B под C) безобидны, но если оба применятся
		// без сериализации - дерево замкнётся A -> B -> C -> A.
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var a = Folder.Create(Guid.NewGuid(), ownerId, null, "A", now);
		var c = Folder.Create(Guid.NewGuid(), ownerId, a.Id, "C", now);
		var b = Folder.Create(Guid.NewGuid(), ownerId, null, "B", now);
		await SeedAsync(a, c, b);

		await using var db1 = CreateContext();
		await using var db2 = CreateContext();
		var repo1 = new FolderRepository(db1);
		var repo2 = new FolderRepository(db2);

		var move1 = repo1.MoveAsync(a.Id, ownerId, b.Id, CancellationToken.None); // A -> child of B
		var move2 = repo2.MoveAsync(b.Id, ownerId, c.Id, CancellationToken.None); // B -> child of C
		var outcomes = await Task.WhenAll(move1, move2);

		// Ровно один должен пройти. Какой именно - недетерминировано (зависит от того, кто
		// первым взял advisory lock), но если сериализация не работает, возможен и другой
		// плохой исход - оба Moved (замкнувшийся цикл) - именно это здесь и исключается.
		Assert.Equal(1, outcomes.Count(o => o == FolderMoveOutcome.Moved));
		Assert.Equal(1, outcomes.Count(o => o == FolderMoveOutcome.WouldCreateCycle));

		// Явная проверка результата, а не только по outcome: пройти по дереву от A и убедиться,
		// что мы упираемся в null, а не возвращаемся к уже посещённому узлу.
		await using var verifyDb = CreateContext();
		var parentById = await verifyDb.Folders.AsNoTracking()
			.Where(f => f.OwnerId == ownerId)
			.ToDictionaryAsync(f => f.Id, f => f.ParentFolderId);

		var visited = new HashSet<Guid>();
		Guid? current = a.Id;
		while (current is { } id)
		{
			Assert.True(visited.Add(id), "Cycle detected in the persisted tree.");
			current = parentById[id];
		}
	}

	[Fact]
	public async Task MoveAsync_NameConflictAtDestination_ThrowsRawPostgresUniqueViolation()
	{
		// Эмпирическая проверка формы исключения для этого нового call-site - тот же паттерн,
		// что уже подтверждён для RenameAsync (см. src/Modules/CLAUDE.md): ExecuteUpdateAsync
		// бросает сырой Npgsql.PostgresException, а не обёрнутый DbUpdateException.
		var ownerId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var target = Folder.Create(Guid.NewGuid(), ownerId, null, "Target", now);
		var existing = Folder.Create(Guid.NewGuid(), ownerId, target.Id, "Documents", now);
		var mover = Folder.Create(Guid.NewGuid(), ownerId, null, "Documents", now);
		await SeedAsync(target, existing, mover);

		await using var db = CreateContext();
		var sut = new FolderRepository(db);

		var ex = await Assert.ThrowsAsync<PostgresException>(
			() => sut.MoveAsync(mover.Id, ownerId, target.Id, CancellationToken.None));

		Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
	}
}
