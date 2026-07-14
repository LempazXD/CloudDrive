using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
	: IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
	public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		builder.HasDefaultSchema("auth");
		builder.ApplyConfiguration(new RefreshTokenConfiguration());
		builder.ApplyConfiguration(new ApplicationUserConfiguration());
	}
}
