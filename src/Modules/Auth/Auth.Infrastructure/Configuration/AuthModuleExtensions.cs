using System.Threading.RateLimiting;
using Auth.Core.Application.Abstractions;
using Auth.Infrastructure.Application;
using Auth.Infrastructure.Caching;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
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
		services.AddJwtOptions(configuration);
		services.AddRateLimitingOptions(configuration);
		services.AddIdentityConfigOptions(configuration);

		services.AddOptions<RateLimiterOptions>()
			.Configure<IOptions<RateLimitingOptions>>((rlOptions, authRateLimiting) =>
			{
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.Login, authRateLimiting.Value.Login);
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.Register, authRateLimiting.Value.Register);
			});

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

		services.AddMemoryCache();
		services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
		services.AddSingleton<IRefreshTokenReplayCache, RefreshTokenReplayCache>();
		services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
		services.AddScoped<IAuthService, AuthService>();

		return services;
	}

	public static async Task MigrateAuthModuleAsync(this IServiceProvider services)
	{
		await using var scope = services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
	}

	private static void AddJwtOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<JwtOptions>()
			.Bind(configuration.GetSection("Jwt"))
			.Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
			.Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
			.Validate(o => IsValidSigningKey(o.SigningKey), "Jwt:SigningKey must be a Base64 string decoding to at least 32 bytes (256 bits).")
			.Validate(o => o.AccessTokenLifetime > TimeSpan.Zero, "Jwt:AccessTokenLifetime must be positive.")
			.Validate(o => o.RefreshTokenLifetime > TimeSpan.Zero, "Jwt:RefreshTokenLifetime must be positive.")
			.Validate(o => o.RefreshTokenReuseGracePeriod >= TimeSpan.Zero, "Jwt:RefreshTokenReuseGracePeriod must not be negative.")
			.ValidateOnStart();
	}

	private static void AddRateLimitingOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<RateLimitingOptions>()
			.Bind(configuration.GetSection("RateLimiting"))
			.Validate(o => o.Login.PermitLimit > 0, "RateLimiting:Login:PermitLimit must be positive.")
			.Validate(o => o.Login.Window > TimeSpan.Zero, "RateLimiting:Login:Window must be positive.")
			.Validate(o => o.Register.PermitLimit > 0, "RateLimiting:Register:PermitLimit must be positive.")
			.Validate(o => o.Register.Window > TimeSpan.Zero, "RateLimiting:Register:Window must be positive.")
			.ValidateOnStart();
	}

	// Биндим на встроенный IdentityOptions, а не свой DTO - Lockout/Password уже есть в Identity.
	// Значения применяются не здесь: UserManager/SignInManager получают тот же IOptions<IdentityOptions>
	// через DI и сами читают Options.Password/Options.Lockout
	// TODO: DefaultLockoutTimeSpan - фиксированная длительность на каждую блокировку
	private static void AddIdentityConfigOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<IdentityOptions>()
			.Bind(configuration.GetSection("Identity"))
			.Validate(o => o.Lockout.MaxFailedAccessAttempts > 0, "Identity:Lockout:MaxFailedAccessAttempts must be positive.")
			.Validate(o => o.Lockout.DefaultLockoutTimeSpan > TimeSpan.Zero, "Identity:Lockout:DefaultLockoutTimeSpan must be positive.")
			.Validate(o => o.Password.RequiredLength > 0, "Identity:Password:RequiredLength must be positive.")
			.Validate(o => o.Password.RequiredUniqueChars > 0, "Identity:Password:RequiredUniqueChars must be positive.")
			.ValidateOnStart();
	}

	private static void AddFixedWindowPolicy(RateLimiterOptions rlOptions, string policyName, RateLimitRuleOptions rule) =>
		rlOptions.AddPolicy(policyName, httpContext =>
			RateLimitPartition.GetFixedWindowLimiter(GetClientIp(httpContext), _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = rule.PermitLimit,
				Window = rule.Window,
				QueueLimit = 0
			}));

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

	// TODO: RemoteIpAddress корректен, только пока API открыт наружу напрямую (как сейчас).
	// Если перед ним появится reverse proxy/LB, сюда будет прилетать IP прокси у всех запросов
	// подряд - все клиенты схлопнутся в один rate-limit bucket.
	private static string GetClientIp(HttpContext httpContext) =>
		httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
