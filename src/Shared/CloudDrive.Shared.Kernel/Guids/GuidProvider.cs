namespace CloudDrive.Shared.Kernel.Guids;

internal sealed class GuidProvider(TimeProvider timeProvider) : IGuidProvider
{
	public Guid CreateVersion7() => Guid.CreateVersion7(timeProvider.GetUtcNow());
}