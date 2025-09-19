using CloudDrive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudDrive.Infrastructure.Persistence.Configurations;

public class TemporaryRegistrationDataEntityConfiguration : IEntityTypeConfiguration<TemporaryRegistrationDataEntity> // !!! Убрать Entity в названии?
{
	public void Configure(EntityTypeBuilder<TemporaryRegistrationDataEntity> builder)
	{
		builder.HasKey(a => a.Id);
	}
}
