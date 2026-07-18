using Auth.Core.Application.Abstractions;
using Auth.Infrastructure.Application;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
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
	public UserManager<ApplicationUser> UserManager { get; }
	public SignInManager<ApplicationUser> SignInManager { get; }
	public IJwtTokenGenerator JwtTokenGenerator { get; } = Substitute.For<IJwtTokenGenerator>();
	public IRefreshTokenRepository RefreshTokenRepository { get; } = Substitute.For<IRefreshTokenRepository>();
	public IRefreshTokenReplayCache ReplayCache { get; } = Substitute.For<IRefreshTokenReplayCache>();
	public IGuidProvider GuidProvider { get; } = Substitute.For<IGuidProvider>();
	public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

	public JwtOptions JwtOptions { get; } = new()
	{
		Issuer = "test-issuer",
		Audience = "test-audience",
		SigningKey = Convert.ToBase64String(new byte[32]),
		AccessTokenLifetime = TimeSpan.FromMinutes(15),
		RefreshTokenLifetime = TimeSpan.FromDays(30)
	};

	public AuthServiceTestHarness()
	{
		UserManager = IdentityMockFactory.CreateUserManager(UserStore);
		SignInManager = IdentityMockFactory.CreateSignInManager(UserManager);
	}

	public IAuthService CreateSut() => new AuthService(
		UserManager,
		SignInManager,
		JwtTokenGenerator,
		RefreshTokenRepository,
		ReplayCache,
		GuidProvider,
		TimeProvider,
		Options.Create(JwtOptions));
}
