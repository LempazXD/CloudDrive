namespace Files.Endpoints.Contracts;

public sealed record FolderResponse(Guid Id, Guid? ParentFolderId, string Name, DateTimeOffset CreatedAtUtc);
