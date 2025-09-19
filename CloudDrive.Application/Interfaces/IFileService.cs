using CloudDrive.Application.DTOs;

namespace CloudDrive.Application.Interfaces;

public interface IFileService
{
	Task<List<FileDto>> GetUserFiles(Guid userId);
	Task<FileDto?> GetFileById(Guid fileId);
	Task UploadFile(Guid userId, string fileName, Stream fileStream);
	Task MoveToTrash(Guid fileId);
	Task RestoreFromTrash(Guid fileId);
	Task DeletePermanently(Guid fileId);
}
