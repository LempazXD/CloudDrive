namespace Files.Endpoints.Contracts;

public sealed record InitiateUploadResponse(Guid FileId, string? UploadId, IReadOnlyList<UploadPartResponse> Parts);
