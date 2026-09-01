using Files.Core.Application.Abstractions;
using Files.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Files.Infrastructure.Application;

internal sealed class TrashPurgeService(
	IStoredFileRepository storedFileRepository,
	IBlobStorage blobStorage,
	TimeProvider timeProvider,
	IOptions<TrashOptions> options,
	ILogger<TrashPurgeService> logger) : ITrashPurgeService
{
	public async Task<TrashPurgeSummary> PurgeExpiredBatchAsync(CancellationToken ct)
	{
		var cutoff = timeProvider.GetUtcNow() - options.Value.RetentionPeriod;
		var candidates = await storedFileRepository.ListExpiredTrashAsync(cutoff, options.Value.PurgeBatchSize, ct);

		var purged = 0;
		var reclaimedBytes = 0L;
		var failed = 0;

		foreach (var file in candidates)
		{
			bool dbPurged;
			try
			{
				// Перепроверяет cutoff в самом WHERE, а не только на момент выборки выше - если файл
				// восстановили, или восстановили и тут же удалили заново, между ListExpiredTrashAsync
				// и этим вызовом, условие не совпадёт и строка останется нетронутой.
				dbPurged = await storedFileRepository.PurgeIfStillExpiredAsync(file.Id, file.OwnerId, cutoff, ct);
			}
			catch (Exception ex)
			{
				failed++;
				logger.LogError(ex, "Failed to purge DB row for trashed file {FileId}.", file.Id);
				continue;
			}

			if (!dbPurged)
				continue; // словили гонку с restore (или restore+delete) - строку и блоб не трогаем

			purged++;
			reclaimedBytes += file.SizeBytes;

			try
			{
				await blobStorage.DeleteObjectAsync(file.StorageKey, ct);
			}
			catch (Exception ex)
			{
				// Строка уже удалена - ретраить нечего, объект в хранилище может остаться сиротой.
				// Отдельное сообщение от сбоя выше специально: там ещё есть что повторить на
				// следующем тике, здесь уже нет.
				logger.LogError(
					ex,
					"DB row for file {FileId} was purged, but its storage object {StorageKey} could not be deleted and may be orphaned.",
					file.Id, file.StorageKey);
			}
		}

		if (purged > 0 || failed > 0)
		{
			logger.LogInformation(
				"Trash sweep: purged {Purged} file(s) ({ReclaimedBytes} bytes reclaimed), {Failed} failure(s).",
				purged, reclaimedBytes, failed);
		}

		return new TrashPurgeSummary(purged, reclaimedBytes, failed);
	}
}
