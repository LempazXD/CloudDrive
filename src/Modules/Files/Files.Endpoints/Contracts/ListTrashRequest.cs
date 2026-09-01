namespace Files.Endpoints.Contracts;

public sealed record ListTrashRequest(string? Cursor, int? Limit);
