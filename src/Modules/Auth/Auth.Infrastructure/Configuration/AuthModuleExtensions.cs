using System.Threading.RateLimiting;
using Auth.Core.Application.Abstractions;
using Auth.Infrastructure.Application;
using Auth.Infrastructure.Caching;
using Auth.Infrastructure.Email;
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
		services.AddSmtpOptions(configuration);
		services.AddRegistrationOptions(configuration);
		services.AddPasswordResetOptions(configuration);
		services.AddPasswordChangeOptions(configuration);

		services.AddOptions<RateLimiterOptions>()
			.Configure<IOptions<RateLimitingOptions>>((rlOptions, authRateLimiting) =>
			{
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.Login, authRateLimiting.Value.Login, GetClientIp);
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.Register, authRateLimiting.Value.Register, GetClientIp);
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.ConfirmRegistration, authRateLimiting.Value.ConfirmRegistration, GetClientIp);
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.ForgotPassword, authRateLimiting.Value.ForgotPassword, GetClientIp);
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.ResetPassword, authRateLimiting.Value.ResetPassword, GetClientIp);
				// По UserId, а не по IP: оба эндпоинта аутентифицированы, и партиция по IP защищала бы
				// не того - мешала бы атакующему бить много ЧУЖИХ аккаунтов с одного адреса, а не бить
				// один конкретный аккаунт через ротацию IP. Технически возможно только потому, что
				// UseAuthentication() в Program.cs идёт раньше UseRateLimiter() - к этому моменту
				// HttpContext.User уже содержит настоящий claims-principal, а не анонимный.
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.ChangePassword, authRateLimiting.Value.ChangePassword, GetUserId);
				AddFixedWindowPolicy(rlOptions, AuthRateLimitPolicies.ConfirmChangePassword, authRateLimiting.Value.ConfirmChangePassword, GetUserId);
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
		services.AddSingleton<IEmailSender, SmtpEmailSender>();
		services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
		services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepository>();
		services.AddScoped<IPendingPasswordResetRepository, PendingPasswordResetRepository>();
		services.AddScoped<IPendingPasswordChangeRepository, PendingPasswordChangeRepository>();
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
			.Validate(o => o.ConfirmRegistration.PermitLimit > 0, "RateLimiting:ConfirmRegistration:PermitLimit must be positive.")
			.Validate(o => o.ConfirmRegistration.Window > TimeSpan.Zero, "RateLimiting:ConfirmRegistration:Window must be positive.")
			.Validate(o => o.ForgotPassword.PermitLimit > 0, "RateLimiting:ForgotPassword:PermitLimit must be positive.")
			.Validate(o => o.ForgotPassword.Window > TimeSpan.Zero, "RateLimiting:ForgotPassword:Window must be positive.")
			.Validate(o => o.ResetPassword.PermitLimit > 0, "RateLimiting:ResetPassword:PermitLimit must be positive.")
			.Validate(o => o.ResetPassword.Window > TimeSpan.Zero, "RateLimiting:ResetPassword:Window must be positive.")
			.Validate(o => o.ChangePassword.PermitLimit > 0, "RateLimiting:ChangePassword:PermitLimit must be positive.")
			.Validate(o => o.ChangePassword.Window > TimeSpan.Zero, "RateLimiting:ChangePassword:Window must be positive.")
			.Validate(o => o.ConfirmChangePassword.PermitLimit > 0, "RateLimiting:ConfirmChangePassword:PermitLimit must be positive.")
			.Validate(o => o.ConfirmChangePassword.Window > TimeSpan.Zero, "RateLimiting:ConfirmChangePassword:Window must be positive.")
			.ValidateOnStart();
	}

	private static void AddSmtpOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<SmtpOptions>()
			.Bind(configuration.GetSection("Smtp"))
			.Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Smtp:Host is required.")
			.Validate(o => o.Port is > 0 and <= 65535, "Smtp:Port must be a valid port number.")
			.Validate(o => !string.IsNullOrWhiteSpace(o.FromAddress), "Smtp:FromAddress is required.")
			.ValidateOnStart();
	}

	private static void AddRegistrationOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<RegistrationOptions>()
			.Bind(configuration.GetSection("Registration"))
			.Validate(o => o.CodeLifetime > TimeSpan.Zero, "Registration:CodeLifetime must be positive.")
			.Validate(o => o.MaxAttempts > 0, "Registration:MaxAttempts must be positive.")
			.ValidateOnStart();
	}

	private static void AddPasswordResetOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<PasswordResetOptions>()
			.Bind(configuration.GetSection("PasswordReset"))
			.Validate(o => o.CodeLifetime > TimeSpan.Zero, "PasswordReset:CodeLifetime must be positive.")
			.Validate(o => o.MaxAttempts > 0, "PasswordReset:MaxAttempts must be positive.")
			.ValidateOnStart();
	}

	// Биндим на встроенный IdentityOptions, а не свой DTO - Lockout/Password уже есть в Identity.
	// Значения применяются не здесь: UserManager/SignInManager получают тот же IOptions<IdentityOptions>
	// через DI и сами читают Options.Password/Options.Lockout
	// TODO: DefaultLockoutTimeSpan - фиксированная длительность на каждую блокировку
	private static void AddPasswordChangeOptions(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<PasswordChangeOptions>()
			.Bind(configuration.GetSection("PasswordChange"))
			.Validate(o => o.CodeLifetime > TimeSpan.Zero, "PasswordChange:CodeLifetime must be positive.")
			.Validate(o => o.MaxAttempts > 0, "PasswordChange:MaxAttempts must be positive.")
			.ValidateOnStart();
	}

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

	private static void AddFixedWindowPolicy(
		RateLimiterOptions rlOptions, string policyName, RateLimitRuleOptions rule, Func<HttpContext, string> partitionKey) =>
		rlOptions.AddPolicy(policyName, httpContext =>
			RateLimitPartition.GetFixedWindowLimiter(partitionKey(httpContext), _ => new FixedWindowRateLimiterOptions
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

	// Только для аутентифицированных политик (ChangePassword/ConfirmChangePassword): оба эндпоинта
	// требуют RequireAuthorization(), а в конвейере Program.cs UseAuthentication() идёт раньше
	// UseRateLimiter() - "sub" всегда присутствует к этому моменту, поэтому без fallback на IP,
	// как и ClaimsPrincipalExtensions.GetUserId() в Auth.Endpoints.
	private static string GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst("sub")?.Value
		?? throw new InvalidOperationException("Missing 'sub' claim.");
}
