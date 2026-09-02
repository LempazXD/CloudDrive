using Amazon.S3;
using Files.Core.Application.Abstractions;
using Files.Infrastructure.Application;
using Files.Infrastructure.BackgroundJobs;
using Files.Infrastructure.Persistence;
using Files.Infrastructure.Storage;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Files.Infrastructure.Configuration;

public static class FilesModuleExtensions
{
	public static IServiceCollection AddFilesModule(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddObjectStorageOptions(configuration);
		services.AddTrashOptions(configuration);

		services.AddDbContext<FilesDbContext>((sp, options) =>
			options.UseNpgsql(
				sp.GetRequiredService<NpgsqlDataSource>(),
				npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "files")));

		services.AddSingleton<IAmazonS3>(sp =>
		{
			var storageOptions = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
			return new AmazonS3Client(
				storageOptions.AccessKey, storageOptions.SecretKey, ObjectStorageOptions.BuildS3Config(storageOptions.Endpoint));
		});

		services.AddScoped<IStoredFileRepository, StoredFileRepository>();
		services.AddScoped<IFolderRepository, FolderRepository>();
		services.AddSingleton<IBlobStorage, SeaweedFsBlobStorage>();
		services.AddScoped<IFilesService, FilesService>();
		services.AddScoped<IFolderService, FolderService>();
		services.AddScoped<ITrashPurgeService, TrashPurgeService>();
		services.AddScoped<TrashPurgeRecurringJob>();

		return services;
	}

	public static async Task MigrateFilesModuleAsync(this IServiceProvider services)
	{
		await using var scope = services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<FilesDbContext>().Database.MigrateAsync();
	}

	// AddOrUpdate идемпотентен - безопасно вызывать на каждом старте, не только один раз.
	public static void ScheduleFilesModuleJobs(this IServiceProvider services)
	{
		var recurringJobs = services.GetRequiredService<IRecurringJobManager>();
		var purgeInterval = services.GetRequiredService<IOptions<TrashOptions>>().Value.PurgeInterval;

		recurringJobs.AddOrUpdate<TrashPurgeRecurringJob>(
			"files-trash-purge", job => job.RunAsync(CancellationToken.None), ToCronExpression(purgeInterval));
	}

	private static void AddObjectStorageOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<ObjectStorageOptions>()
			.Bind(configuration.GetSection("ObjectStorage"))
			.Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint), "ObjectStorage:Endpoint is required.")
			.Validate(o => !string.IsNullOrWhiteSpace(o.AccessKey), "ObjectStorage:AccessKey is required.")
			.Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey), "ObjectStorage:SecretKey is required.")
			.Validate(o => !string.IsNullOrWhiteSpace(o.Bucket), "ObjectStorage:Bucket is required.")
			.Validate(o => o.MultipartThresholdBytes > 0, "ObjectStorage:MultipartThresholdBytes must be positive.")
			.Validate(o => o.PresignedUploadTtl > TimeSpan.Zero, "ObjectStorage:PresignedUploadTtl must be positive.")
			.Validate(o => o.PresignedDownloadTtl > TimeSpan.Zero, "ObjectStorage:PresignedDownloadTtl must be positive.")
			.Validate(o => o.CompletionStaleAfter > TimeSpan.Zero, "ObjectStorage:CompletionStaleAfter must be positive.")
			.Validate<IHostEnvironment>(
				(o, env) => env.IsDevelopment() || o.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
				"ObjectStorage:Endpoint must use https outside Development.")
			.Validate<IHostEnvironment>(
				(o, env) => env.IsDevelopment() || o.EffectivePublicEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
				"ObjectStorage:PublicEndpoint (or Endpoint, if unset) must use https outside Development.")
			.ValidateOnStart();
	}

	private static void AddTrashOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<TrashOptions>()
			.Bind(configuration.GetSection("Trash"))
			.Validate(o => o.RetentionPeriod > TimeSpan.Zero, "Trash:RetentionPeriod must be positive.")
			.Validate(o => o.PurgeInterval > TimeSpan.Zero, "Trash:PurgeInterval must be positive.")
			.Validate(
				IsValidPurgeInterval,
				"Trash:PurgeInterval must be a whole number of hours dividing evenly into 24, or (if under an hour) a whole number of minutes dividing evenly into 60 - a limitation of cron's */N syntax, which Hangfire's recurring-job schedule is built from.")
			.Validate(o => o.PurgeBatchSize > 0, "Trash:PurgeBatchSize must be positive.")
			.ValidateOnStart();
	}

	// Cron's */N syntax means "when the field is a multiple of N", not "N units after the last run" -
	// only interval values that divide evenly into their field's range repeat at a truly constant
	// gap (Cron.HourInterval(5), for example, fires at 0/5/10/15/20, a short 4-hour gap back to 0).
	private static bool IsValidPurgeInterval(TrashOptions options) =>
		options.PurgeInterval.TotalHours >= 1
			? options.PurgeInterval.TotalHours == Math.Floor(options.PurgeInterval.TotalHours) && 24 % (int)options.PurgeInterval.TotalHours == 0
			: options.PurgeInterval.TotalMinutes == Math.Floor(options.PurgeInterval.TotalMinutes) && 60 % (int)options.PurgeInterval.TotalMinutes == 0;

	private static string ToCronExpression(TimeSpan interval) =>
		interval.TotalHours >= 1 ? Cron.HourInterval((int)interval.TotalHours) : Cron.MinuteInterval((int)interval.TotalMinutes);
}
