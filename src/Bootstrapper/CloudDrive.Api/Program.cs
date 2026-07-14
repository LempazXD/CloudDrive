using Shared.Api.Extensions;
using Shared.Kernel.Guids;
using Auth.Endpoints;
using Auth.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddSingleton(TimeProvider.System)
	.AddSingleton<IGuidProvider, GuidProvider>()
	.AddProblemDetailsConfiguration()
	.AddGlobalExceptionHandling()
	.AddRequestLocalizationConfiguration()
	.AddOpenApi()
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer();

builder.Services.AddAuthorization();

builder.Services.AddNpgsqlDataSource(
	builder.Configuration.GetConnectionString("CloudDrive")
		?? throw new InvalidOperationException("Connection string 'CloudDrive' is not configured."));

builder.Services.AddAuthModule(builder.Configuration);

builder.Services
	.AddHealthChecks()
	.AddNpgSql(
		sp => sp.GetRequiredService<NpgsqlDataSource>(),
		name: "postgresql",
		tags: ["ready"]);

var app = builder.Build();

// ValidateOnStart() срабатывает только внутри app.RunAsync() (через hosted service), то есть
// уже после миграции ниже - без явного вызова здесь битая конфигурация (например, Jwt:SigningKey)
// не помешает миграции успеть изменить схему БД до падения приложения. Выполняется безусловно
// (не только когда включены миграции), чтобы битый конфиг был пойман как можно раньше в любом окружении.
app.Services.GetRequiredService<IStartupValidator>().Validate();

if (builder.Configuration.GetValue<bool>("Migrations:ApplyOnStartup"))
	await app.Services.MigrateAuthModuleAsync();

app.UseRequestLocalizationConfiguration();
app.UseGlobalExceptionHandling();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	Predicate = static check => check.Tags.Contains("ready"),
});

// TODO: добавить rate limiting (AddRateLimiter/UseRateLimiter) для auth-эндпоинтов (login/register)
// Там изменить статус по умолчанию на 429
// Также добавить информацию через сколько можно попробовать снова
// И добавить группировку (по IP, userId либо как ещё)
app.MapAuthEndpoints();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference();  // localhost:5166/scalar
}

await app.RunAsync();
