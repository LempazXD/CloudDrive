using CloudDrive.Domain.Entities;

namespace CloudDrive.Domain.Interfaces;

public interface IFileRepository
{
	Task<List<FileEntity>> GetAllByUserId(Guid userId);
	Task<FileEntity?> GetOneById(Guid id);
	Task MoveToTrashById(Guid id);
	Task RestoreById(Guid id);
	Task DeletePermanentlyById(Guid id);
	Task Add(FileEntity file);
	Task SaveChanges();
}
