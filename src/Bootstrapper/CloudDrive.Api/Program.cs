using CloudDrive.Shared.Api.Extensions;
using CloudDrive.Shared.Kernel.Guids;

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddSingleton(TimeProvider.System)
	.AddSingleton<IGuidProvider, GuidProvider>()
	.AddProblemDetailsConfiguration()
	.AddGlobalExceptionHandling()
	.AddRequestLocalizationConfiguration();

var app = builder.Build();

app.UseRequestLocalizationConfiguration();
app.UseGlobalExceptionHandling();

app.Run();