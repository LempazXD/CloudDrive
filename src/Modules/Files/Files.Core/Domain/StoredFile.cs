namespace Files.Core.Domain;

public sealed class StoredFile
{
	private StoredFile() { }

	public Guid Id { get; private set; }

	public Guid OwnerId { get; private set; }

	public Guid? FolderId { get; private set; }

	public string OriginalFileName { get; private set; } = null!;

	public string ContentType { get; private set; } = null!;

	public long SizeBytes { get; private set; }

	/// <summary> SHA-256, заявленный клиентом при initiate. Информационный - серверная сторона его не пересчитывает. </summary>
	public string Sha256Declared { get; private set; } = null!;

	public string StorageKey { get; private set; } = null!;

	/// <summary> S3 multipart upload id. Null - загрузка была одиночным PUT (не превысила порог multipart). </summary>
	public string? UploadId { get; private set; }

	/// <summary> Число presigned-частей, выданных при initiate. Используется на complete для проверки, что клиент отчитался ровно за все части. </summary>
	public int ExpectedPartCount { get; private set; }

	public FileStatus Status { get; private set; }

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset UpdatedAtUtc { get; private set; }

	public static StoredFile Create(
		Guid id,
		Guid ownerId,
		Guid? folderId,
		string originalFileName,
		string contentType,
		long sizeBytes,
		string sha256Declared,
		string storageKey,
		string? uploadId,
		int expectedPartCount,
		DateTimeOffset nowUtc) =>
		new()
		{
			Id = id,
			OwnerId = ownerId,
			FolderId = folderId,
			OriginalFileName = originalFileName,
			ContentType = contentType,
			SizeBytes = sizeBytes,
			Sha256Declared = sha256Declared,
			StorageKey = storageKey,
			UploadId = uploadId,
			ExpectedPartCount = expectedPartCount,
			Status = FileStatus.Pending,
			CreatedAtUtc = nowUtc,
			UpdatedAtUtc = nowUtc
		};
}
