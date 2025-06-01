namespace CloudDrive.Domain.Entities;

public class FileEntity
{
	public int Id { get; set; }
	public string OriginalName { get; set; } = default!;
	public string StorageName { get; set; } = default!; // Guid + OriginalName = Имя файла в хранилище
	public bool IsFolder { get; set; }
	public string Extension { get; set; } = default!;
	public long Size { get; set; } = default!;
	public string Path { get; set; } = default!;
	public DateTime UploadDate { get; set; }
	public bool IsDeleted { get; set; }
	public DateTime? DeletionTime { get; set; }

	public int? ParentId { get; set; }
	public FileEntity? Parent { get; set; }
	public ICollection<FileEntity> Children { get; set; } = new List<FileEntity>();

	public int UserId { get; set; }
	public UserEntity User { get; set; }
}
