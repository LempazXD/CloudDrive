using CloudDrive.Domain.Entities;
using CloudDrive.Domain.Enums;
using CloudDrive.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CloudDrive.Infrastructure.Persistence.Repositories;

public class FileRepository : IFileRepository
{
	private readonly CloudDriveDbContext _context;

	public FileRepository(CloudDriveDbContext context)
	{
		_context = context;
	}

	public async Task Add(FileEntity file)
	{
		await _context.AddAsync(file);
	}

	public async Task DeletePermanentlyById(Guid id)
	{
		await _context.Files
			.Where(f => f.Id == id)
			.ExecuteDeleteAsync();
	}

	public async Task<List<FileEntity>> GetAllByUserId(Guid userId)
	{
		return await _context.Files
			.Where(f => f.UserId == userId)
			.ToListAsync();
	}

	public async Task<FileEntity?> GetOneById(Guid id)
	{
		return await _context.Files
			.Where(f => f.Id == id)
			.FirstOrDefaultAsync();
	}

	public async Task MoveToTrashById(Guid id)
	{
		await _context.Files
			.Where(f => f.Id == id)
			.ExecuteUpdateAsync(s => s
				.SetProperty(f => f.Status, FileStatusType.Deleted)
				.SetProperty(f => f.DeletionTime, DateTime.UtcNow));
	}

	public async Task RestoreById(Guid id)
	{
		await _context.Files
			.Where(f => f.Id == id)
			.ExecuteUpdateAsync(s => s
				.SetProperty(f => f.Status, FileStatusType.Completed)
				.SetProperty(f => f.DeletionTime, (DateTime?)null));
	}

	public async Task SaveChanges()
	{
		await _context.SaveChangesAsync();
	}
}
