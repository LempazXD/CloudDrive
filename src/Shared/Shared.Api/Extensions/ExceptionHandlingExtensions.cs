using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api.ExceptionHandling;

namespace Shared.Api.Extensions;

public static class ExceptionHandlingExtensions
{
	public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
	{
		services.AddExceptionHandler<GlobalExceptionHandler>();
		return services;
	}

	public static WebApplication UseGlobalExceptionHandling(this WebApplication app)
	{
		app.UseExceptionHandler();
		app.UseStatusCodePages();
		return app;
	}
}
