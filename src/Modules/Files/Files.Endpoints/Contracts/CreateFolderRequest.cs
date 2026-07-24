namespace Files.Endpoints.Contracts;

public sealed record CreateFolderRequest(Guid? ParentFolderId, string Name);
