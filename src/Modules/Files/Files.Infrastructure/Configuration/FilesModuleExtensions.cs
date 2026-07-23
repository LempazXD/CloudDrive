using Amazon.S3;
using Files.Core.Application.Abstractions;
using Files.Infrastructure.Application;
using Files.Infrastructure.Persistence;
using Files.Infrastructure.Storage;
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

		return services;
	}

	public static async Task MigrateFilesModuleAsync(this IServiceProvider services)
	{
		await using var scope = services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<FilesDbContext>().Database.MigrateAsync();
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
}
