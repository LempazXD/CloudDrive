using CloudDrive.Shared.Api.Extensions;
using CloudDrive.Shared.Kernel.Guids;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddSingleton(TimeProvider.System)
	.AddSingleton<IGuidProvider, GuidProvider>()
	.AddProblemDetailsConfiguration()
	.AddGlobalExceptionHandling()
	.AddRequestLocalizationConfiguration()
	.AddOpenApi();

builder.Services.AddNpgsqlDataSource(
	builder.Configuration.GetConnectionString("CloudDrive")
		?? throw new InvalidOperationException("Connection string 'CloudDrive' is not configured."));

builder.Services
	.AddHealthChecks()
	.AddNpgSql(
		sp => sp.GetRequiredService<NpgsqlDataSource>(),
		name: "postgresql",
		tags: ["ready"]);

var app = builder.Build();

app.UseRequestLocalizationConfiguration();
app.UseGlobalExceptionHandling();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	Predicate = static check => check.Tags.Contains("ready"),
});

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference();  // localhost:5166/scalar
}

app.Run();
