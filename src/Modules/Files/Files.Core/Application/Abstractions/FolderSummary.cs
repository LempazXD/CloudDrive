namespace Files.Core.Application.Abstractions;

public sealed record FolderSummary(Guid Id, Guid? ParentFolderId, string Name, DateTimeOffset CreatedAtUtc);
