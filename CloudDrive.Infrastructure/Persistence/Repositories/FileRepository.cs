using CloudDrive.Domain.Entities;
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

	public async Task DeletePermanentlyById(int id)
	{
		await _context.Files
			.Where(f => f.Id == id)
			.ExecuteDeleteAsync();
	}

	public async Task<List<FileEntity>> GetAllByUserId(int userId)
	{
		return await _context.Files
			.Where(f => f.UserId == userId)
			.ToListAsync();
	}

	public async Task<FileEntity?> GetOneById(int id)
	{
		return await _context.Files
			.Where(f => f.Id == id)
			.FirstOrDefaultAsync();
	}

	public async Task MoveToTrashById(int id)
	{
		await _context.Files
			.Where(f => f.Id == id)
			.ExecuteUpdateAsync(s => s
				.SetProperty(f => f.IsDeleted, true)
				.SetProperty(f => f.DeletionTime, DateTime.UtcNow));
	}

	public async Task RestoreById(int id)
	{
		await _context.Files
			.Where(f => f.Id == id)
			.ExecuteUpdateAsync(s => s
				.SetProperty(f => f.IsDeleted, false)
				.SetProperty(f => f.DeletionTime, (DateTime?)null));
	}

	public async Task SaveChanges()
	{
		await _context.SaveChangesAsync();
	}
}
