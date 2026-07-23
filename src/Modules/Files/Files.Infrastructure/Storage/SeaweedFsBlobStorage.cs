using Amazon.S3;
using Amazon.S3.Model;
using Files.Core.Application.Abstractions;
using Files.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Files.Infrastructure.Storage;

internal sealed class SeaweedFsBlobStorage(IAmazonS3 s3Client, IOptions<ObjectStorageOptions> options) : IBlobStorage, IDisposable
{
	// s3Client обращается к ObjectStorage:Endpoint - он доступен изнутри
	// собственной сети API. Presigned-ссылки, напротив, отдаются внешнему вызывающему, которому
	// нужен публично доступный endpoint - отсюда второй, только для presign, клиент, построенный
	// из EffectivePublicEndpoint. Создать его здесь дёшево: presign не делает сетевых вызовов.
	private readonly AmazonS3Client _presignClient = new(
		options.Value.AccessKey,
		options.Value.SecretKey,
		ObjectStorageOptions.BuildS3Config(options.Value.EffectivePublicEndpoint));

	// AmazonS3Config.UseHttp влияет только на схему *реальных* запросов SDK; генерация presigned-
	// ссылок читает схему из GetPreSignedUrlRequest.Protocol независимо, по умолчанию - HTTPS,
	// вне зависимости от UseHttp - поэтому её тоже нужно выставлять явно.
	private readonly Protocol _presignProtocol = options.Value.EffectivePublicEndpoint
		.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
		? Protocol.HTTPS
		: Protocol.HTTP;

	public async Task<BlobUploadTarget> InitiateUploadAsync(
		string storageKey, string contentType, long sizeBytes, CancellationToken ct)
	{
		var config = options.Value;

		if (sizeBytes <= config.MultipartThresholdBytes)
		{
			var url = await _presignClient.GetPreSignedURLAsync(new GetPreSignedUrlRequest
			{
				BucketName = config.Bucket,
				Key = storageKey,
				Verb = HttpVerb.PUT,
				Protocol = _presignProtocol,
				ContentType = contentType,
				Expires = DateTime.UtcNow.Add(config.PresignedUploadTtl)
			});

			return new BlobUploadTarget(UploadId: null, Parts: [new BlobUploadPart(1, url)]);
		}

		var initiateResponse = await s3Client.InitiateMultipartUploadAsync(
			new InitiateMultipartUploadRequest
			{
				BucketName = config.Bucket,
				Key = storageKey,
				ContentType = contentType
			},
			ct);

		var partCount = (int)Math.Ceiling(sizeBytes / (double)config.MultipartThresholdBytes);
		var parts = new List<BlobUploadPart>(partCount);

		for (var partNumber = 1; partNumber <= partCount; partNumber++)
		{
			var url = await _presignClient.GetPreSignedURLAsync(new GetPreSignedUrlRequest
			{
				BucketName = config.Bucket,
				Key = storageKey,
				Verb = HttpVerb.PUT,
				Protocol = _presignProtocol,
				UploadId = initiateResponse.UploadId,
				PartNumber = partNumber,
				Expires = DateTime.UtcNow.Add(config.PresignedUploadTtl)
			});

			parts.Add(new BlobUploadPart(partNumber, url));
		}

		return new BlobUploadTarget(initiateResponse.UploadId, parts);
	}

	public async Task<BlobObjectInfo> CompleteUploadAsync(
		string storageKey, string? uploadId, IReadOnlyList<BlobUploadedPart> parts, CancellationToken ct)
	{
		var bucket = options.Value.Bucket;

		if (uploadId is null)
		{
			var singlePutMetadata = await s3Client.GetObjectMetadataAsync(bucket, storageKey, ct);
			return new BlobObjectInfo(singlePutMetadata.ETag, singlePutMetadata.ContentLength);
		}

		var completeResponse = await s3Client.CompleteMultipartUploadAsync(
			new CompleteMultipartUploadRequest
			{
				BucketName = bucket,
				Key = storageKey,
				UploadId = uploadId,
				PartETags = parts.Select(p => new PartETag(p.PartNumber, p.ETag)).ToList()
			},
			ct);

		// CompleteMultipartUploadResponse не сообщает итоговый размер - берём его отдельным HEAD.
		var metadata = await s3Client.GetObjectMetadataAsync(bucket, storageKey, ct);
		return new BlobObjectInfo(completeResponse.ETag, metadata.ContentLength);
	}

	public async Task<string> GetPresignedDownloadUrlAsync(
		string storageKey, string downloadFileName, string contentType, CancellationToken ct)
	{
		var config = options.Value;

		var request = new GetPreSignedUrlRequest
		{
			BucketName = config.Bucket,
			Key = storageKey,
			Verb = HttpVerb.GET,
			Protocol = _presignProtocol,
			Expires = DateTime.UtcNow.Add(config.PresignedDownloadTtl)
		};
		request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename=\"{downloadFileName}\"";
		request.ResponseHeaderOverrides.ContentType = contentType;

		return await _presignClient.GetPreSignedURLAsync(request);
	}

	public Task DeleteObjectAsync(string storageKey, CancellationToken ct) =>
		s3Client.DeleteObjectAsync(options.Value.Bucket, storageKey, ct);

	public async Task<bool> IsAvailableAsync(CancellationToken ct)
	{
		try
		{
			await s3Client.ListBucketsAsync(new ListBucketsRequest(), ct);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public void Dispose() => _presignClient.Dispose();
}
