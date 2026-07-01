using CloudDrive.Shared.Api.ExceptionHandling;
using CloudDrive.Shared.Api.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace CloudDrive.Shared.Api.Extensions;

public static class ProblemDetailsExtensions
{
	public static IServiceCollection AddProblemDetailsConfiguration(this IServiceCollection services)
	{
		services.AddSingleton<IErrorLocalizer, JsonErrorLocalizer>();

		services.AddProblemDetails(options =>
		{
			options.CustomizeProblemDetails = context =>
			{
				ProblemDetailsEnricher.Enrich(context);
				ProblemDetailsLocalizer.Localize(context);
			};
		});

		return services;
	}
}
