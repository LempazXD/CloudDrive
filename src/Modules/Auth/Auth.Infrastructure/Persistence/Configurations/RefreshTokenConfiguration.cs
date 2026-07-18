using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
	public void Configure(EntityTypeBuilder<RefreshToken> builder)
	{
		builder.ToTable("RefreshTokens");
		builder.HasKey(t => t.Id);
		builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
		builder.HasIndex(t => t.TokenHash).IsUnique();

		// Соответствует фильтру (UserId, RevokedAtUtc IS NULL), по которому строит запрос
		// RevokeAllForUserAsync, поэтому отзыв сканирует только активные токены пользователя,
		// а не всю его историю.
		builder.HasIndex(t => t.UserId).HasFilter("\"RevokedAtUtc\" IS NULL");

		// Аналогично - под RevokeSessionAsync, которым реально пользуется reuse-detection.
		builder.HasIndex(t => t.SessionId).HasFilter("\"RevokedAtUtc\" IS NULL");

		builder.HasOne<ApplicationUser>()
			.WithMany()
			.HasForeignKey(t => t.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Property(t => t.CreatedAtUtc).IsRequired();
		builder.Property(t => t.ExpiresAtUtc).IsRequired();

		// Подстраховка от бага при выпуске токена (например, перепутанный TimeSpan в
		// IssueTokensAsync) - не может подловить "токен истёк только что", так как CHECK
		// проверяется лишь при INSERT/UPDATE строки, а не пересчитывается с течением времени.
		builder.ToTable(t => t.HasCheckConstraint(
			"CK_RefreshTokens_ExpiresAfterCreated",
			"\"ExpiresAtUtc\" > \"CreatedAtUtc\""));
	}
}
