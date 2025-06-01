using CloudDrive.Domain.Entities;
using CloudDrive.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace CloudDrive.Infrastructure.Persistence;

public class CloudDriveDbContext : DbContext
{
	public DbSet<UserEntity> Users => Set<UserEntity>();
	public DbSet<FileEntity> Files => Set<FileEntity>();
	public DbSet<AuthCodeEntity> AuthCodes => Set<AuthCodeEntity>();

	public CloudDriveDbContext(DbContextOptions options)
		: base(options) { }


	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new FileEntityConfiguration());
		modelBuilder.ApplyConfiguration(new UserEntityConfiguration());
		modelBuilder.ApplyConfiguration(new AuthCodeEntityConfiguration());

		base.OnModelCreating(modelBuilder);
	}
}
