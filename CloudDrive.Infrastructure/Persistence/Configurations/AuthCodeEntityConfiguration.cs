using CloudDrive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudDrive.Infrastructure.Persistence.Configurations;

public class AuthCodeEntityConfiguration : IEntityTypeConfiguration<AuthCodeEntity>
{
	public void Configure(EntityTypeBuilder<AuthCodeEntity> builder)
	{
		builder.HasKey(a => a.Id);

		builder.HasIndex(a => a.UsernameOrEmail)
			.IsUnique();

		builder.Property(a => a.Code)
			.IsRequired()
			.HasMaxLength(6);

		builder.Property(a => a.CreatedAt)
			.HasDefaultValue(DateTime.UtcNow);

		builder.Property(a => a.FailedAttempts)
			.HasDefaultValue(0);

		builder.Property(a => a.SentCodeCount)
			.HasDefaultValue(0);
	}
}
