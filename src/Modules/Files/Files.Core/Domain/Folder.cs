namespace Files.Core.Domain;

public sealed class Folder
{
	private Folder() { }

	public Guid Id { get; private set; }

	public Guid OwnerId { get; private set; }

	public Guid? ParentFolderId { get; private set; }

	public string Name { get; private set; } = null!;

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public static Folder Create(
		Guid id,
		Guid ownerId,
		Guid? parentFolderId,
		string name,
		DateTimeOffset createdAtUtc) =>
		new()
		{
			Id = id,
			OwnerId = ownerId,
			ParentFolderId = parentFolderId,
			Name = name,
			CreatedAtUtc = createdAtUtc
		};
}
