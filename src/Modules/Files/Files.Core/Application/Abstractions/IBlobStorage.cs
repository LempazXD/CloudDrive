namespace Files.Core.Application.Abstractions;

public interface IBlobStorage
{
	Task<BlobUploadTarget> InitiateUploadAsync(
		string storageKey, string contentType, long sizeBytes, CancellationToken ct);

	Task<BlobObjectInfo> CompleteUploadAsync(
		string storageKey, string? uploadId, IReadOnlyList<BlobUploadedPart> parts, CancellationToken ct);

	Task<string> GetPresignedDownloadUrlAsync(
		string storageKey, string downloadFileName, string contentType, CancellationToken ct);

	Task DeleteObjectAsync(string storageKey, CancellationToken ct);

	Task<bool> IsAvailableAsync(CancellationToken ct);
}

/// <summary> Presigned PUT-ссылка(и) на части объекта. <paramref name="UploadId"/> - null для одиночного PUT (размер не превысил порог multipart). </summary>
public sealed record BlobUploadTarget(string? UploadId, IReadOnlyList<BlobUploadPart> Parts);

public sealed record BlobUploadPart(int PartNumber, string PresignedUrl);

/// <summary> Часть, о загрузке которой отчитался клиент: номер + ETag, который хранилище вернуло на PUT этой части. </summary>
public sealed record BlobUploadedPart(int PartNumber, string ETag);

public sealed record BlobObjectInfo(string ETag, long SizeBytes);
