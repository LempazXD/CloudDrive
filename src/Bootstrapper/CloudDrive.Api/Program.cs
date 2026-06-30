using CloudDrive.Shared.Api.Extensions;
using CloudDrive.Shared.Kernel.Guids;

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddSingleton(TimeProvider.System)
	.AddSingleton<IGuidProvider, GuidProvider>()
	.AddProblemDetailsConfiguration()
	.AddGlobalExceptionHandling()
	.AddRequestLocalizationConfiguration();

builder.Services.AddNpgsqlDataSource(
	builder.Configuration.GetConnectionString("CloudDrive")
		?? throw new InvalidOperationException("Connection string 'CloudDrive' is not configured."));

var app = builder.Build();

app.UseRequestLocalizationConfiguration();
app.UseGlobalExceptionHandling();

app.Run();