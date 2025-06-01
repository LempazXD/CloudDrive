using CloudDrive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudDrive.Infrastructure.Persistence.Configurations;

public class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
	public void Configure(EntityTypeBuilder<UserEntity> builder)
	{
		builder.HasKey(u => u.Id);

		builder.HasIndex(u => u.Username).IsUnique();
		builder.HasIndex(u => u.Email).IsUnique();
	}
}
