namespace Files.Endpoints.Contracts;

public sealed record InitiateUploadRequest(
	Guid? FolderId, string OriginalFileName, string ContentType, long SizeBytes, string Sha256Declared);
