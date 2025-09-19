using CloudDrive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudDrive.Infrastructure.Persistence.Configurations;

public class MailCodeEntityConfiguration : IEntityTypeConfiguration<MailCodeEntity> // !!! Убрать Entity в названии?
{
	public void Configure(EntityTypeBuilder<MailCodeEntity> builder)
	{
		builder.HasKey(a => a.Id);

		builder.HasIndex(a => a.Email)
			.IsUnique();

		builder.Property(a => a.Code)
			.IsRequired()
			.HasMaxLength(6);
	}
}
