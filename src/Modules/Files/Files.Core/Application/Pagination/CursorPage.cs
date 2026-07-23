namespace Files.Core.Application.Pagination;

public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
