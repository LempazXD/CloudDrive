using CloudDrive.Domain.Entities;
using CloudDrive.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CloudDrive.Infrastructure.Persistence.Repositories;

public class MailCodeRepository : IMailCodeRepository
{
	private readonly CloudDriveDbContext _context;

	public MailCodeRepository(CloudDriveDbContext context)
	{
		_context = context;
	}

	public async Task<MailCodeEntity?> FindByEmail(string email)
	{
		return await _context.MailCodes.FirstOrDefaultAsync(a => a.Email == email);
	}

	public async Task Add(MailCodeEntity authCode)
	{
		await _context.MailCodes.AddAsync(authCode);
	}

	public void Update(MailCodeEntity authCode)
	{
		_context.MailCodes.Update(authCode);
	}

	public async Task SaveChanges()
	{
		await _context.SaveChangesAsync();
	}
}
