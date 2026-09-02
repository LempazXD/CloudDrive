using Files.Core.Application.Abstractions;
using Hangfire;

namespace Files.Infrastructure.BackgroundJobs;

// Hangfire резолвит этот тип через ASP.NET Core DI (зарегистрирован Scoped в AddFilesModule) и сам
// управляет scope на вызов - в отличие от PeriodicTimer-предшественника, здесь не нужен
// IServiceScopeFactory вручную.
internal sealed class TrashPurgeRecurringJob(ITrashPurgeService purgeService)
{
	// Потолок на дренаж бэклога за один запуск - без него один прогон с большим бэклогом мог бы
	// крутиться бесконечно, отжимая процесс от следующего запланированного срабатывания.
	private const int MaxIterationsPerRun = 50;

	// Замена той сериализации, что раньше давал сам PeriodicTimer-цикл (следующий тик не мог
	// начаться, пока не отработал предыдущий) - без атрибута, если один прогон подвиснет дольше
	// расписания, Hangfire мог бы запустить второй экземпляр параллельно.
	[DisableConcurrentExecution(timeoutInSeconds: 60)]
	public async Task RunAsync(CancellationToken ct)
	{
		for (var i = 0; i < MaxIterationsPerRun && !ct.IsCancellationRequested; i++)
		{
			var summary = await purgeService.PurgeExpiredBatchAsync(ct);

			if (summary.PurgedCount == 0)
				break;
		}
	}
}
