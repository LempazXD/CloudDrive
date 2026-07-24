namespace Files.Endpoints.Contracts;

public sealed record CompletedPartRequest(int PartNumber, string ETag);
