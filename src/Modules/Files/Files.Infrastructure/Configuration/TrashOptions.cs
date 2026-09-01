namespace Files.Infrastructure.Configuration;

public sealed class TrashOptions
{
	/// <summary> Сколько файл хранится в корзине, прежде чем будет доступен для окончательного удаления. </summary>
	public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromDays(30);
}
