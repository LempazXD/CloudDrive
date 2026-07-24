namespace Files.Endpoints.Contracts;

public sealed record FileListResponse(IReadOnlyList<FileResponse> Items, string? NextCursor);
