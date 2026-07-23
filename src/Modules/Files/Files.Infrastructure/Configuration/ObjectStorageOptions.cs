using Amazon.S3;

namespace Files.Infrastructure.Configuration;

public sealed class ObjectStorageOptions
{
	public required string Endpoint { get; init; }

	/// <summary>
	/// Endpoint, встраиваемый в presigned URL (initiate/download), если отличается от <see cref="Endpoint"/>.
	/// В Docker <see cref="Endpoint"/> указывает на внутреннее имя сервиса (например,
	/// http://clouddrive.storage:8333) - оно недостижимо для реального клиента, который получает
	/// presigned URL и обращается к хранилищу напрямую снаружи сети контейнеров. Null - значит
	/// оба совпадают (например, в host-режиме, где Endpoint и так localhost).
	/// </summary>
	public string? PublicEndpoint { get; init; }

	public required string AccessKey { get; init; }

	public required string SecretKey { get; init; }

	public required string Bucket { get; init; }

	/// <summary> Порог, выше которого InitiateUploadAsync открывает multipart upload вместо одного presigned PUT. </summary>
	public long MultipartThresholdBytes { get; init; } = 50 * 1024 * 1024; // 50 MB

	/// <summary> Срок жизни presigned URL на загрузку (PUT). </summary>
	public TimeSpan PresignedUploadTtl { get; init; } = TimeSpan.FromMinutes(15);

	/// <summary> Срок жизни presigned URL на скачивание (GET). </summary>
	public TimeSpan PresignedDownloadTtl { get; init; } = TimeSpan.FromMinutes(5);

	/// <summary> Окно, за которое строка, застрявшая в Completing (падение процесса на середине complete), становится доступной для повторного захвата. </summary>
	public TimeSpan CompletionStaleAfter { get; init; } = TimeSpan.FromMinutes(2);

	public string EffectivePublicEndpoint => string.IsNullOrWhiteSpace(PublicEndpoint) ? Endpoint : PublicEndpoint;

	public static AmazonS3Config BuildS3Config(string endpoint) => new()
	{
		ServiceURL = endpoint,
		ForcePathStyle = true,
		UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
	};
}
