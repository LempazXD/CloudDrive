using CloudDrive.Domain.Entities;
using CloudDrive.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CloudDrive.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
	private readonly CloudDriveDbContext _context;

	public UserRepository(CloudDriveDbContext context)
	{
		_context = context;
	}

	public async Task<UserEntity?> FindByUsername(string username)
	{
		return await _context.Users
			.FirstOrDefaultAsync(u => u.Username == username);
	}

	public async Task<UserEntity?> FindByEmail(string email)
	{
		return await _context.Users
			.FirstOrDefaultAsync(u => u.Email == email);
	}

	public async Task<bool> UserExistsByUsernameOrEmail(string username, string email)
	{
		return await _context.Users
			.AnyAsync(u => u.Username == username || u.Email == email);
	}

	public async Task SaveChanges()
	{
		await _context.SaveChangesAsync();
	}

	public async Task Add(UserEntity user)
	{
		await _context.Users.AddAsync(user);
	}
}
