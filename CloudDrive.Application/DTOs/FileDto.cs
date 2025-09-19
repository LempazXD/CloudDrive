namespace CloudDrive.Application.DTOs;

public class FileDto
{
	public int Id { get; set; }
	public string OriginalName { get; set; } = default!;
	public string StorageName { get; set; } = default!;
	public bool IsFolder { get; set; }
	public string Extension { get; set; } = default!;
	public long Size { get; set; }
	public string Path { get; set; } = default!;
	public int? ParentId { get; set; }
	public bool IsDeleted { get; set; }
	public DateTime? DeletionTime { get; set; }
	public DateTime UploadDate { get; set; }
	public int UserId { get; set; }
	public string IconUrl { get; set; } = default!;
}
