using CloudDrive.Shared.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddProblemDetailsConfiguration()
	.AddGlobalExceptionHandling()
	.AddRequestLocalizationConfiguration();

var app = builder.Build();

app.UseRequestLocalizationConfiguration();
app.UseGlobalExceptionHandling();

app.Run();