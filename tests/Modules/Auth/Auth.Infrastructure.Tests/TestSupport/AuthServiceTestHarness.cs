using Auth.Core.Application.Abstractions;
using Auth.Infrastructure.Application;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shared.Kernel.Guids;

namespace Auth.Infrastructure.Tests.TestSupport;

/// <summary>
/// Собирает все зависимости <see cref="AuthService"/> как NSubstitute-моки (кроме
/// <see cref="TimeProvider"/> и <see cref="JwtOptions"/> — их проще использовать настоящими) и
/// строит сам SUT. Каждый тест создаёт свой экземпляр, поэтому моки не расшарены между тестами.
/// </summary>
internal sealed class AuthServiceTestHarness
{
	public IUserStore<ApplicationUser> UserStore { get; } = Substitute.For<IUserStore<ApplicationUser>>();
	public IPasswordHasher<ApplicationUser> PasswordHasher { get; } = Substitute.For<IPasswordHasher<ApplicationUser>>();
	public IUserValidator<ApplicationUser> UserValidator { get; } = Substitute.For<IUserValidator<ApplicationUser>>();
	public IPasswordValidator<ApplicationUser> PasswordValidator { get; } = Substitute.For<IPasswordValidator<ApplicationUser>>();
	public UserManager<ApplicationUser> UserManager { get; }
	public SignInManager<ApplicationUser> SignInManager { get; }
	public IJwtTokenGenerator JwtTokenGenerator { get; } = Substitute.For<IJwtTokenGenerator>();
	public IRefreshTokenRepository RefreshTokenRepository { get; } = Substitute.For<IRefreshTokenRepository>();
	public IRefreshTokenReplayCache ReplayCache { get; } = Substitute.For<IRefreshTokenReplayCache>();
	public IPendingRegistrationRepository PendingRegistrationRepository { get; } = Substitute.For<IPendingRegistrationRepository>();
	public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();
	public IGuidProvider GuidProvider { get; } = Substitute.For<IGuidProvider>();
	public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	public ILogger<AuthService> Logger { get; } = Substitute.For<ILogger<AuthService>>();

	public JwtOptions JwtOptions { get; } = new()
	{
		Issuer = "test-issuer",
		Audience = "test-audience",
		SigningKey = Convert.ToBase64String(new byte[32]),
		AccessTokenLifetime = TimeSpan.FromMinutes(15),
		RefreshTokenLifetime = TimeSpan.FromDays(30)
	};

	public RegistrationOptions RegistrationOptions { get; } = new()
	{
		CodeLifetime = TimeSpan.FromMinutes(15),
		MaxAttempts = 5
	};

	public AuthServiceTestHarness()
	{
		UserManager = IdentityMockFactory.CreateUserManager(UserStore, PasswordHasher, [UserValidator], [PasswordValidator]);
		SignInManager = IdentityMockFactory.CreateSignInManager(UserManager);

		// Дефолты для веток RegisterAsync/ConfirmRegistrationAsync, общих почти для всех тестов на них;
		// точечно переопределяются в тестах, которым важно конкретное значение.
		UserManager.NormalizeEmail(Arg.Any<string>()).Returns(ci => ci.Arg<string>()!.ToUpperInvariant());
		PasswordHasher.HashPassword(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns("hashed-password");
	}

	public IAuthService CreateSut() => new AuthService(
		UserManager,
		SignInManager,
		JwtTokenGenerator,
		RefreshTokenRepository,
		ReplayCache,
		PendingRegistrationRepository,
		EmailSender,
		GuidProvider,
		TimeProvider,
		Options.Create(JwtOptions),
		Options.Create(RegistrationOptions),
		Logger);
}
