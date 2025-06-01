using CloudDrive.Domain.Entities;
using CloudDrive.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CloudDrive.Infrastructure.Persistence.Repositories;

public class AuthCodeRepository : IAuthCodeRepository
{
	private readonly CloudDriveDbContext _context;

	public AuthCodeRepository(CloudDriveDbContext context)
	{
		_context = context;
	}

	public async Task<AuthCodeEntity?> FindByUsernameOrEmail(string usernameOrEmail)
	{
		return await _context.AuthCodes
			.FirstOrDefaultAsync(a => a.UsernameOrEmail == usernameOrEmail);
	}

	public async Task Add(AuthCodeEntity authCode)
	{
		await _context.AuthCodes.AddAsync(authCode);
	}

	public void Update(AuthCodeEntity authCode)
	{
		_context.AuthCodes.Update(authCode);
	}

	public async Task SaveChanges()
	{
		await _context.SaveChangesAsync();
	}
}
