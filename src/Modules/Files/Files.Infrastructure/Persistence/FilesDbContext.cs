using Files.Core.Domain;
using Files.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Files.Infrastructure.Persistence;

public sealed class FilesDbContext(DbContextOptions<FilesDbContext> options) : DbContext(options)
{
	public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

	public DbSet<Folder> Folders => Set<Folder>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.HasDefaultSchema("files");
		modelBuilder.ApplyConfiguration(new StoredFileConfiguration());
		modelBuilder.ApplyConfiguration(new FolderConfiguration());
	}
}
