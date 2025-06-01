using CloudDrive.Application.DTOs;

namespace CloudDrive.Application.Interfaces;

public interface IFileManager
{
	Task<List<FileDto>> GetFiles(int userId);
	Task<FileDto?> GetFileById(int fileId);
	Task UploadFile(FileDto fileDto);
	Task MoveToTrash(int fileId);
	Task RestoreFromTrash(int fileId);
	Task DeletePermanently(int fileId);
}
