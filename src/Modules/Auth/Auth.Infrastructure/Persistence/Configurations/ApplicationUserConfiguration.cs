using Auth.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
	public void Configure(EntityTypeBuilder<ApplicationUser> builder)
	{
		// RequireUniqueEmail в UserManager - check-then-insert, не атомарно со вставкой.
		// Этот уникальный индекс - страховка от гонки при параллельной регистрации
		// (Identity по умолчанию создаёт EmailIndex не уникальным). Порядок важен:
		// применяется после base.OnModelCreating в AuthDbContext.
		builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("EmailIndex");
	}
}
