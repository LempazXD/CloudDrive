using Microsoft.Extensions.DependencyInjection;
using Shared.Api.ExceptionHandling;
using Shared.Api.Localization;

namespace Shared.Api.Extensions;

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
				RetryAfterEnricher.Enrich(context);
			};
		});

		return services;
	}
}
