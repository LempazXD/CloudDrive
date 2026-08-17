using Auth.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

internal sealed class PendingRegistrationConfiguration : IEntityTypeConfiguration<PendingRegistration>
{
	public void Configure(EntityTypeBuilder<PendingRegistration> builder)
	{
		builder.ToTable("PendingRegistrations");
		builder.HasKey(p => p.Id);

		builder.Property(p => p.NormalizedEmail).IsRequired().HasMaxLength(256);

		// Одна активная заявка на email: RegisterAsync удаляет старую перед вставкой новой (upsert),
		// индекс - страховка от гонки двух параллельных /register на один и тот же email.
		builder.HasIndex(p => p.NormalizedEmail).IsUnique().HasDatabaseName("PendingRegistrationEmailIndex");

		builder.Property(p => p.Email).IsRequired().HasMaxLength(256);
		builder.Property(p => p.Username).IsRequired().HasMaxLength(256);
		builder.Property(p => p.CodeHash).IsRequired().HasMaxLength(64);
		builder.Property(p => p.PasswordHash).IsRequired();

		// Поддерживает DeleteExpiredAsync (WHERE ExpiresAtUtc <= @now) без full scan по мере роста таблицы.
		builder.HasIndex(p => p.ExpiresAtUtc);

		builder.Property(p => p.CreatedAtUtc).IsRequired();
		builder.Property(p => p.ExpiresAtUtc).IsRequired();

		builder.ToTable(t => t.HasCheckConstraint(
			"CK_PendingRegistrations_ExpiresAfterCreated",
			"\"ExpiresAtUtc\" > \"CreatedAtUtc\""));
	}
}
