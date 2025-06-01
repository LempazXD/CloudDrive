using CloudDrive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudDrive.Infrastructure.Persistence.Configurations;

public class FileEntityConfiguration : IEntityTypeConfiguration<FileEntity>
{
	public void Configure(EntityTypeBuilder<FileEntity> builder)
	{
		builder.HasKey(f => f.Id);

		builder.HasOne(f => f.Parent)
			.WithMany(f => f.Children)
			.HasForeignKey(f => f.ParentId);

		builder.HasOne(f => f.User)
			.WithMany(u => u.Files)
			.HasForeignKey(f => f.UserId);
	}
}
