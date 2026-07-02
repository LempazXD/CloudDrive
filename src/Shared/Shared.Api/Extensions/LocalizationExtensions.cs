using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Api.Extensions;

public static class LocalizationExtensions
{
	public static IServiceCollection AddRequestLocalizationConfiguration(this IServiceCollection services)
	{
		services.Configure<RequestLocalizationOptions>(options =>
		{
			var supported = new[]
			{
				new CultureInfo("en"),
				new CultureInfo("ru")
			};
			options.DefaultRequestCulture = new RequestCulture("ru");
			options.SupportedCultures = supported;
			options.SupportedUICultures = supported;
		});

		return services;
	}

	// Должен идти перед UseExceptionHandler
	public static WebApplication UseRequestLocalizationConfiguration(this WebApplication app)
	{
		app.UseRequestLocalization();
		return app;
	}
}
