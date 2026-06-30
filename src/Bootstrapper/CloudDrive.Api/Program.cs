using CloudDrive.Shared.Api.Extensions;
using CloudDrive.Shared.Kernel.Guids;
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

var app = builder.Build();

app.UseRequestLocalizationConfiguration();
app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference();  // localhost:5166/scalar 
}

app.Run();
