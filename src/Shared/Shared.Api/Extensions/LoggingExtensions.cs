using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Shared.Api.Logging;

namespace Shared.Api.Extensions;

public static class LoggingExtensions
{
	public static WebApplicationBuilder AddLoggingConfiguration(this WebApplicationBuilder builder)
	{
		builder.Services.AddOptions<SeqOptions>()
			.Bind(builder.Configuration.GetSection("Seq"))
			.Validate(o => SeqOptions.TryGetServerUri(o.ServerUrl, out _), "Seq:ServerUrl must be an absolute URL.")
			.ValidateOnStart();

		builder.Host.UseSerilog((context, services, configuration) => configuration
			.ReadFrom.Configuration(context.Configuration)
			.ReadFrom.Services(services)
			.Enrich.FromLogContext()
			.Enrich.WithSpan()
			.Enrich.WithMachineName()
			.Enrich.WithThreadId()
			.Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
			.WriteTo.Seq(
				services.GetRequiredService<IOptions<SeqOptions>>().Value.ServerUrl,
				formatProvider: CultureInfo.InvariantCulture));

		return builder;
	}

	/// <summary> Должен идти до exception-handling middleware, чтобы длительность запроса охватывала обработку исключений. </summary>
	public static WebApplication UseLoggingConfiguration(this WebApplication app)
	{
		app.UseSerilogRequestLogging(options =>
		{
			// rate-limit отклонение (429) неотличимо от обычного запроса без явного
			// фильтра по StatusCode - поднимаем его до Warning отдельно.
			options.GetLevel = (httpContext, _, ex) => ex is not null || httpContext.Response.StatusCode > 499
				? LogEventLevel.Error
				: httpContext.Response.StatusCode == StatusCodes.Status429TooManyRequests
					? LogEventLevel.Warning
					: LogEventLevel.Information;

			options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
			{
				diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
				diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
			};
		});

		return app;
	}
}
