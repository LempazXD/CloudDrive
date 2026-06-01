using Microsoft.Extensions.DependencyInjection;

namespace CloudDrive.Shared.Kernel.Guids;

public static class GuidProviderServiceCollectionExtensions
{
	public static IServiceCollection AddGuidProvider(this IServiceCollection services) =>
		services.AddSingleton<IGuidProvider, GuidProvider>();
}
