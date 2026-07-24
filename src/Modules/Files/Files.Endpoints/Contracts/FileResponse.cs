namespace Files.Endpoints.Contracts;

public sealed record FileResponse(
	Guid Id,
	Guid? FolderId,
	string OriginalFileName,
	string ContentType,
	long SizeBytes,
	string Status,
	DateTimeOffset CreatedAtUtc);
