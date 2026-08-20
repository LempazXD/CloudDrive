using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

internal sealed class PendingPasswordResetConfiguration : IEntityTypeConfiguration<PendingPasswordReset>
{
	public void Configure(EntityTypeBuilder<PendingPasswordReset> builder)
	{
		builder.ToTable("PendingPasswordResets");
		builder.HasKey(p => p.Id);

		// Одна активная заявка на пользователя: ForgotPasswordAsync удаляет старую перед вставкой
		// новой (upsert), индекс - страховка от гонки двух параллельных /forgot-password подряд.
		builder.HasIndex(p => p.UserId).IsUnique().HasDatabaseName("PendingPasswordResetUserIndex");

		builder.HasOne<ApplicationUser>()
			.WithMany()
			.HasForeignKey(p => p.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Property(p => p.CodeHash).IsRequired().HasMaxLength(64);

		// Поддерживает DeleteExpiredAsync (WHERE ExpiresAtUtc <= @now) без full scan по мере роста таблицы.
		builder.HasIndex(p => p.ExpiresAtUtc);

		builder.Property(p => p.CreatedAtUtc).IsRequired();
		builder.Property(p => p.ExpiresAtUtc).IsRequired();

		builder.ToTable(t => t.HasCheckConstraint(
			"CK_PendingPasswordResets_ExpiresAfterCreated",
			"\"ExpiresAtUtc\" > \"CreatedAtUtc\""));
	}
}
