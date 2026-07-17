using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Api.Logging;

namespace Shared.Api.Extensions;

public static class OpenTelemetryExtensions
{
	public static WebApplicationBuilder AddOpenTelemetryConfiguration(this WebApplicationBuilder builder)
	{
		if (!SeqOptions.TryGetServerUri(builder.Configuration["Seq:ServerUrl"], out var seqServerUri))
			throw new InvalidOperationException("Seq:ServerUrl must be configured as an absolute URL.");

		void ConfigureOtlpExporter(OtlpExporterOptions options, string signalPath)
		{
			options.Endpoint = new Uri(seqServerUri, signalPath);
			options.Protocol = OtlpExportProtocol.HttpProtobuf;
		}

		builder.Services.AddOpenTelemetry()
			.ConfigureResource(resource => resource
				.AddService(builder.Environment.ApplicationName)
				.AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName)]))
			.WithTracing(tracing => tracing
				.AddAspNetCoreInstrumentation(ExcludeHealthChecks)
				.AddHttpClientInstrumentation()
				.AddNpgsql()
				.AddOtlpExporter(options => ConfigureOtlpExporter(options, "/ingest/otlp/v1/traces")))
			.WithMetrics(metrics => metrics
				.AddAspNetCoreInstrumentation()
				.AddHttpClientInstrumentation()
				.AddRuntimeInstrumentation()
				.AddMeter("Microsoft.AspNetCore.RateLimiting")
				.AddMeter("Microsoft.AspNetCore.Server.Kestrel")
				.AddOtlpExporter(options => ConfigureOtlpExporter(options, "/ingest/otlp/v1/metrics")));

		return builder;
	}

	// Health-check polling would otherwise dominate trace volume in Seq without adding diagnostic value.
	private static void ExcludeHealthChecks(AspNetCoreTraceInstrumentationOptions options) =>
		options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
}
