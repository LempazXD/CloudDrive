namespace Files.Endpoints.Contracts;

public sealed record UploadPartResponse(int PartNumber, string PresignedUrl);
