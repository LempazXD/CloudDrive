namespace Files.Core.Application.Abstractions;

public sealed record InitiateUploadResult(Guid FileId, string? UploadId, IReadOnlyList<BlobUploadPart> Parts);
