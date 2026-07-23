using Files.Core.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Files.Infrastructure.Storage;

public sealed class ObjectStorageHealthCheck(IBlobStorage blobStorage) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context, CancellationToken cancellationToken = default) =>
		await blobStorage.IsAvailableAsync(cancellationToken)
			? HealthCheckResult.Healthy()
			: HealthCheckResult.Unhealthy();
}
