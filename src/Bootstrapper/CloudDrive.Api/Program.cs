using CloudDrive.Shared.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddProblemDetailsConfiguration()
	.AddGlobalExceptionHandling();

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.Run();