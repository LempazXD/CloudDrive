using Auth.Core.Application.Abstractions;
using Auth.Infrastructure.Application;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Auth.Infrastructure.Configuration;

public static class AuthModuleExtensions
{
	public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<JwtOptions>()
			.Bind(configuration.GetSection("Jwt"))
			.Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
			.Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
			.Validate(o => IsValidSigningKey(o.SigningKey), "Jwt:SigningKey must be a Base64 string decoding to at least 32 bytes (256 bits).")
			.Validate(o => o.AccessTokenLifetime > TimeSpan.Zero, "Jwt:AccessTokenLifetime must be positive.")
			.Validate(o => o.RefreshTokenLifetime > TimeSpan.Zero, "Jwt:RefreshTokenLifetime must be positive.")
			.ValidateOnStart();

		services.AddDbContext<AuthDbContext>((sp, options) =>
			options.UseNpgsql(
				sp.GetRequiredService<NpgsqlDataSource>(),
				npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "auth")));

		services
			.AddIdentityCore<ApplicationUser>(options =>
			{
				options.User.RequireUniqueEmail = true;
			})
			.AddEntityFrameworkStores<AuthDbContext>()
			.AddSignInManager()
			.AddDefaultTokenProviders();

		services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
			.Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
			{
				var options = jwtOptions.Value;
				bearerOptions.MapInboundClaims = false;
				bearerOptions.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidIssuer = options.Issuer,
					ValidateAudience = true,
					ValidAudience = options.Audience,
					ValidateLifetime = true,
					ClockSkew = TimeSpan.Zero,
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = options.GetSecurityKey()
				};
			});

		services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
		services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
		services.AddScoped<IAuthService, AuthService>();

		return services;
	}

	public static async Task MigrateAuthModuleAsync(this IServiceProvider services)
	{
		await using var scope = services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
	}

	private static bool IsValidSigningKey(string signingKey)
	{
		if (string.IsNullOrWhiteSpace(signingKey))
			return false;

		try
		{
			return Convert.FromBase64String(signingKey).Length >= 32;
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
