using CloudDrive.Shared.Api.ExceptionHandling;
using Microsoft.Extensions.DependencyInjection;

namespace CloudDrive.Shared.Api.Extensions;

public static class ProblemDetailsExtensions
{
	public static IServiceCollection AddProblemDetailsConfiguration(this IServiceCollection services)
	{
		services.AddProblemDetails(options =>
		{
			options.CustomizeProblemDetails = ProblemDetailsEnricher.Enrich;
		});

		return services;
	}
}