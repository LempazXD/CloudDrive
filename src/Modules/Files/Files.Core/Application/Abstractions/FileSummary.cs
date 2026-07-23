using Files.Core.Domain;

namespace Files.Core.Application.Abstractions;

public sealed record FileSummary(
	Guid Id,
	Guid? FolderId,
	string OriginalFileName,
	string ContentType,
	long SizeBytes,
	FileStatus Status,
	DateTimeOffset CreatedAtUtc);
