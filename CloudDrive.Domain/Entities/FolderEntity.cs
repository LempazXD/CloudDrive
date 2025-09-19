using CloudDrive.Domain.Enums;
using Microsoft.AspNetCore.StaticFiles; // Для FileExtensionContentTypeProvider

namespace CloudDrive.Domain.Entities;

public class FileEntity
{
	private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

	public Guid Id { get; private set; }

	// --- File info ---
	public string OriginalName { get; private set; }
	public string StorageName { get; private set; } // Guid.NewGuid().ToString("N")
	public string Extension { get; private set; } // Для UI
	public string? ContentType { get; private set; } // Для логики и безопасности
	public long? Size { get; private set; }
	public string? Hash { get; private set; }
	public DateTime? UploadDate { get; private set; }
	public FileStatusType Status { get; private set; }
	public DateTime? DeletionTime { get; private set; }

	// --- Hierarchy ---
	public bool IsFolder { get; private set; }
	public Guid? ParentId { get; private set; }
	public FileEntity? Parent { get; private set; }
	public ICollection<FileEntity> Children { get; private set; } = new List<FileEntity>();

	// --- Ownership ---
	public Guid UserId { get; private set; }
	public UserEntity User { get; private set; }

	private FileEntity() { } // Для EF Core

	private FileEntity(
		string originalName,
		string storageName,
		string extension,
		string contentType,
		bool isFolder,
		long? size,
		string? hash,
		DateTime? uploadDate,
		FileStatusType status,
		DateTime? deletionTime,
		Guid parentId,
		Guid userId)
	{
		OriginalName = originalName;
		StorageName = storageName;
		Extension = extension;
		ContentType = contentType;
		IsFolder = isFolder;
		Size = size;
		Hash = hash;
		UploadDate = uploadDate;
		Status = status;
		DeletionTime = deletionTime;
		ParentId = parentId;
		UserId = userId;
	}

	public static FileEntity Create(string origName)
	{
		var extension = Path.GetExtension(origName) ?? string.Empty;
		var contentType = GetContentType(origName);
		var isFolder = string.IsNullOrEmpty(extension);

		return new FileEntity(
			origName,
			Guid.NewGuid().ToString("N"),
			extension,
			contentType,
			isFolder,
			0,
			null!,
			null,
			FileStatusType.Pending,
			null,
			Guid.Empty,
			Guid.Empty);
	}

	private static string GetContentType(string fileName)
	{
		return _contentTypeProvider.TryGetContentType(fileName, out var contentType)
			? contentType
			: "application/octet-stream";
	}

	// --- Domain methods ---
	public void MarkAsDeleted()
	{
		Status = FileStatusType.Deleted;
		DeletionTime = DateTime.Now;
	}
	public void Restore()
	{
		Status = FileStatusType.Completed;
		DeletionTime = null;
	}

	public void MarkAsFailed()
	{
		Status = FileStatusType.Failed;
	}

	public void Rename(string newName)
	{
		if (string.IsNullOrEmpty(newName))
			throw new ArgumentNullException("Имя файла не может быть пустым", nameof(newName)); // Зачем здесь писать nameof(newName)

		if (string.Equals(Path.GetExtension(OriginalName), Path.GetExtension(newName)))
			throw new Exception(); // Спрашивать у пользователя, действительно ли он хочет поменять расширение файла?

		OriginalName = newName;
		Extension = Path.GetExtension(newName) ?? string.Empty; // Стоит ли оставить расширение файла пустым? У папок расширение тоже пустое, тогда папкам можно сделать расширение "Folder"
		ContentType = GetContentType(newName);
		StorageName = Guid.NewGuid().ToString("N"); // Зачем пересоздавать Storagename
	}
}
