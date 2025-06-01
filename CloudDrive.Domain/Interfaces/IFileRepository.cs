using CloudDrive.Domain.Entities;

namespace CloudDrive.Domain.Interfaces;

public interface IFileRepository
{
	Task<List<FileEntity>> GetAllByUserId(int userId);
	Task<FileEntity?> GetOneById(int id);
	Task MoveToTrashById(int id);
	Task RestoreById(int id);
	Task DeletePermanentlyById(int id);
	Task Add(FileEntity file);
	Task SaveChanges();
}
