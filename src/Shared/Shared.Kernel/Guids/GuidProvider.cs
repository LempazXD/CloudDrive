using System.ComponentModel;

namespace Shared.Kernel.Guids;

/// <summary>
/// Реализация <see cref="IGuidProvider"/> по умолчанию. Не создавайте напрямую —
/// получайте <see cref="IGuidProvider"/> через DI. Public только для того, чтобы composition root мог его зарегистрировать.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GuidProvider(TimeProvider timeProvider) : IGuidProvider
{
	public Guid CreateVersion7() => Guid.CreateVersion7(timeProvider.GetUtcNow());
}
