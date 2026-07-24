namespace Files.Endpoints.Contracts;

public sealed record ListFilesRequest(Guid? FolderId, string? Cursor, int? Limit);
