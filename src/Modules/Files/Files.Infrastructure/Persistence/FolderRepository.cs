using Files.Core.Application.Abstractions;
using Files.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Files.Infrastructure.Persistence;

internal sealed class FolderRepository(FilesDbContext db) : IFolderRepository
{
	// Произвольная константа-неймспейс для advisory-lock ключей этой фичи - защищает от
	// коллизии, если где-то ещё в приложении в будущем появится свой pg_advisory_xact_lock.
	private const long FolderMoveLockNamespace = 0x466F6C64_4D6F7665;

	// Circuit breaker на глубину обхода предков в MoveAsync, а не продуктовое ограничение -
	// CreateFolderAsync глубину не проверяет. Защищает от зависшего запроса, если инвариант
	// "в дереве нет циклов" когда-нибудь всё же будет нарушен (баг, ручная правка в БД). См. ADR 0016.
	private const int MaxAncestorDepth = 10_000;

	public Task AddAsync(Folder folder, CancellationToken ct)
	{
		db.Folders.Add(folder);
		return Task.CompletedTask;
	}

	public Task<Folder?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		db.Folders.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId, ct);

	public Task<bool> ExistsAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		db.Folders.AsNoTracking().AnyAsync(f => f.Id == id && f.OwnerId == ownerId, ct);

	public Task<bool> HasSubfoldersAsync(Guid folderId, CancellationToken ct) =>
		db.Folders.AsNoTracking().AnyAsync(f => f.ParentFolderId == folderId, ct);

	public async Task<bool> RenameAsync(Guid id, Guid ownerId, string newName, CancellationToken ct) =>
		await db.Folders
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteUpdateAsync(s => s.SetProperty(f => f.Name, newName), ct) == 1;

	public async Task<FolderMoveOutcome> MoveAsync(Guid id, Guid ownerId, Guid? newParentFolderId, CancellationToken ct)
	{
		await using var tx = await db.Database.BeginTransactionAsync(ct);

		// Полные 128 бит ownerId, а не hashtext()/32-битный срез - меньше шанс, что двум разным
		// владельцам достанется один ключ и они станут зря сериализоваться друг с другом.
		// System.HashCode.Combine здесь не годится: его сид рандомизируется per-process, поэтому
		// два инстанса API за балансировщиком считали бы для одного owner разные ключи и молча
		// перестали бы сериализоваться между собой.
		var ownerBytes = ownerId.ToByteArray();
		var lockKey = BitConverter.ToInt64(ownerBytes, 0) ^ BitConverter.ToInt64(ownerBytes, 8) ^ FolderMoveLockNamespace;

		// xact-вариант освобождается сам на commit/rollback - не может "утечь", если запрос
		// упадёт на середине. Сериализует только folder-move одного владельца между собой
		// (см. ADR 0016) - не блокирует create/rename/delete и не блокирует другого владельца.
		// Порядок "сначала lock, потом проверка цикла" обязателен - иначе защита от write skew
		// (см. ADR 0016) молча перестаёт работать.
		await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);

		if (newParentFolderId is { } newParentId)
		{
			// true, если id (перемещаемая папка) лежит где-то в цепочке родителей newParentId
			// (включая сам newParentId) - т.е. перемещение id под newParentId замкнуло бы дерево.
			// OwnerId фильтруется на каждом шаге - не потому что сегодня есть путь протечь в
			// чужое поддерево (id и newParentId уже проверены на владельца в FolderService до
			// вызова этого метода, так что вся цепочка по построению принадлежит одному owner),
			// а чтобы корректность запроса не зависела от инварианта, поддерживаемого далеко
			// отсюда, в другом слое.
			var wouldCycle = await db.Database.SqlQuery<bool>($"""
				WITH RECURSIVE ancestors AS (
					SELECT "Id", "ParentFolderId", 0 AS depth
					FROM files."Folders"
					WHERE "Id" = {newParentId} AND "OwnerId" = {ownerId}
					UNION ALL
					SELECT f."Id", f."ParentFolderId", a.depth + 1
					FROM files."Folders" f
					INNER JOIN ancestors a ON f."Id" = a."ParentFolderId"
					WHERE f."OwnerId" = {ownerId} AND a.depth < {MaxAncestorDepth}
				)
				SELECT EXISTS (SELECT 1 FROM ancestors WHERE "Id" = {id})
				""").SingleAsync(ct);

			if (wouldCycle)
				return FolderMoveOutcome.WouldCreateCycle; // tx откатится сам при Dispose - commit не вызывался
		}

		// Unique/FK-violation НЕ ловятся здесь - пробрасываются наверх в FolderService, который
		// ловит их так же, как для rename (см. UniqueConstraintExceptionHelper). await using
		// откатит транзакцию при размотке стека, даже если исключение улетает отсюда.
		var rows = await db.Folders
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteUpdateAsync(s => s.SetProperty(f => f.ParentFolderId, newParentFolderId), ct);

		if (rows != 1)
			return FolderMoveOutcome.NotFound; // гонка: папку удалили между fetch в сервисе и этой записью

		await tx.CommitAsync(ct);
		return FolderMoveOutcome.Moved;
	}

	public async Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken ct) =>
		await db.Folders
			.Where(f => f.Id == id && f.OwnerId == ownerId)
			.ExecuteDeleteAsync(ct) == 1;

	public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
