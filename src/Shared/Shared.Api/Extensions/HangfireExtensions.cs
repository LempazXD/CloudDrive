using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Api.Extensions;

public static class HangfireExtensions
{
	public static WebApplicationBuilder AddHangfireConfiguration(this WebApplicationBuilder builder, string connectionString)
	{
		builder.Services.AddHangfire(config => config
			.UsePostgreSqlStorage(connectionString));
		builder.Services.AddHangfireServer();

		return builder;
	}
}
