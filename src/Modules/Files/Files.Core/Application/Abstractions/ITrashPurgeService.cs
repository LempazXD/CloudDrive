namespace Files.Core.Application.Abstractions;

public interface ITrashPurgeService
{
	/// <summary>
	/// Обрабатывает один батч просроченных файлов из корзины (размер - TrashOptions.PurgeBatchSize):
	/// для каждого пытается условно удалить строку (не трогая её, если файл успели восстановить или
	/// восстановить и удалить заново после исходной выборки), и только при успехе - удаляет объект в
	/// хранилище. Вызывается в цикле планировщиком (TrashPurgeRecurringJob), пока не вернёт пустой
	/// батч - сам метод не планирует повторные вызовы.
	/// </summary>
	Task<TrashPurgeSummary> PurgeExpiredBatchAsync(CancellationToken ct);
}
