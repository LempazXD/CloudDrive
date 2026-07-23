using Files.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Files.Infrastructure.Persistence.Configurations;

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
	public void Configure(EntityTypeBuilder<StoredFile> builder)
	{
		builder.ToTable("StoredFiles");
		builder.HasKey(f => f.Id);

		builder.Property(f => f.OriginalFileName).IsRequired().HasMaxLength(1024);
		builder.Property(f => f.ContentType).IsRequired().HasMaxLength(255);
		builder.Property(f => f.Sha256Declared).IsRequired().HasMaxLength(64);
		builder.Property(f => f.StorageKey).IsRequired().HasMaxLength(200);
		builder.Property(f => f.UploadId).HasMaxLength(500);

		builder.HasIndex(f => f.OwnerId);

		// Postgres считает каждый NULL уникальным для unique-индекса, поэтому один индекс на
		// (OwnerId, FolderId, Name) незаметно допустил бы повторяющиеся имена в корне
		// (FolderId IS NULL). Разбито на два partial-индекса, чтобы покрыть оба случая.
		builder.HasIndex(f => new { f.OwnerId, f.FolderId, f.OriginalFileName })
			.IsUnique()
			.HasFilter("\"FolderId\" IS NOT NULL");

		builder.HasIndex(f => new { f.OwnerId, f.OriginalFileName })
			.IsUnique()
			.HasFilter("\"FolderId\" IS NULL");

		builder.HasOne<Folder>()
			.WithMany()
			.HasForeignKey(f => f.FolderId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.Property(f => f.CreatedAtUtc).IsRequired();
		builder.Property(f => f.UpdatedAtUtc).IsRequired();

		builder.ToTable(t => t.HasCheckConstraint("CK_StoredFiles_SizeBytesPositive", "\"SizeBytes\" > 0"));
	}
}
