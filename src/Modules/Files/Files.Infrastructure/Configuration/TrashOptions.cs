namespace Files.Infrastructure.Configuration;

public sealed class TrashOptions
{
	/// <summary> Сколько файл хранится в корзине, прежде чем фоновая очистка удалит его окончательно. </summary>
	public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromDays(30);

	/// <summary> Сколько файлов обрабатывает один проход очистки, прежде чем отдать управление таймеру. </summary>
	public int PurgeBatchSize { get; init; } = 200;
}
