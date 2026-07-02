using System.ComponentModel;

namespace Shared.Kernel.Guids;

/// <summary>
/// Default <see cref="IGuidProvider"/> implementation. Do not construct directly —
/// resolve <see cref="IGuidProvider"/> from DI. Public only so the composition root can register it.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GuidProvider(TimeProvider timeProvider) : IGuidProvider
{
	public Guid CreateVersion7() => Guid.CreateVersion7(timeProvider.GetUtcNow());
}
