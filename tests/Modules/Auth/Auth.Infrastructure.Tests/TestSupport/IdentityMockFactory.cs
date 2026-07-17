using Auth.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Auth.Infrastructure.Tests.TestSupport;

internal static class IdentityMockFactory
{
	public static UserManager<ApplicationUser> CreateUserManager(IUserStore<ApplicationUser> store) =>
		Substitute.For<UserManager<ApplicationUser>>(
			store,
			Options.Create(new IdentityOptions()),
			Substitute.For<IPasswordHasher<ApplicationUser>>(),
			Array.Empty<IUserValidator<ApplicationUser>>(),
			Array.Empty<IPasswordValidator<ApplicationUser>>(),
			Substitute.For<ILookupNormalizer>(),
			new IdentityErrorDescriber(),
			Substitute.For<IServiceProvider>(),
			Substitute.For<ILogger<UserManager<ApplicationUser>>>());

	public static SignInManager<ApplicationUser> CreateSignInManager(UserManager<ApplicationUser> userManager) =>
		Substitute.For<SignInManager<ApplicationUser>>(
			userManager,
			Substitute.For<IHttpContextAccessor>(),
			Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
			Options.Create(new IdentityOptions()),
			Substitute.For<ILogger<SignInManager<ApplicationUser>>>(),
			Substitute.For<IAuthenticationSchemeProvider>(),
			Substitute.For<IUserConfirmation<ApplicationUser>>());
}
