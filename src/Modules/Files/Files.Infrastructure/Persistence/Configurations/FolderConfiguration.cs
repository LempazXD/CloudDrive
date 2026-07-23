using Files.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Files.Infrastructure.Persistence.Configurations;

internal sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
	public void Configure(EntityTypeBuilder<Folder> builder)
	{
		builder.ToTable("Folders");
		builder.HasKey(f => f.Id);

		builder.Property(f => f.Name).IsRequired().HasMaxLength(255);

		// Та же особенность с NULL, что и в StoredFileConfiguration, - два partial-индекса,
		// чтобы корневые папки (ParentFolderId IS NULL) получили ту же гарантию уникальности.
		builder.HasIndex(f => new { f.OwnerId, f.ParentFolderId, f.Name })
			.IsUnique()
			.HasFilter("\"ParentFolderId\" IS NOT NULL");

		builder.HasIndex(f => new { f.OwnerId, f.Name })
			.IsUnique()
			.HasFilter("\"ParentFolderId\" IS NULL");

		builder.HasOne<Folder>()
			.WithMany()
			.HasForeignKey(f => f.ParentFolderId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.Property(f => f.CreatedAtUtc).IsRequired();
	}
}
