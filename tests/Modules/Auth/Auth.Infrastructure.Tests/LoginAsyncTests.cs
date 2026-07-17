using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class LoginAsyncTests
{
	[Fact]
	public async Task LoginAsync_EmptyLogin_ReturnsInvalidCredentials()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.LoginAsync("", "password", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.User.InvalidCredentials", result.Error.Code);
	}

	[Fact]
	public async Task LoginAsync_EmptyPassword_ReturnsInvalidCredentials()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.LoginAsync("user", "", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.User.InvalidCredentials", result.Error!.Code);
	}

	[Fact]
	public async Task LoginAsync_UnknownLogin_ReturnsInvalidCredentials()
	{
		var harness = new AuthServiceTestHarness();
		harness.UserManager.FindByNameAsync("unknown").Returns((ApplicationUser?)null);
		harness.UserManager.FindByEmailAsync("unknown").Returns((ApplicationUser?)null);
		var sut = harness.CreateSut();

		var result = await sut.LoginAsync("unknown", "password", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.User.InvalidCredentials", result.Error!.Code);
	}

	[Fact]
	public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByNameAsync("user").Returns(user);
		harness.SignInManager.CheckPasswordSignInAsync(user, "wrong-password", true)
			.Returns(SignInResult.Failed);
		var sut = harness.CreateSut();

		var result = await sut.LoginAsync("user", "wrong-password", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.User.InvalidCredentials", result.Error!.Code);
	}

	[Fact]
	public async Task LoginAsync_LockedOutWithKnownEnd_ReturnsLockedOutWithRetryAfter()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		var lockoutEnd = harness.TimeProvider.GetUtcNow().AddMinutes(5);
		harness.UserManager.FindByNameAsync("user").Returns(user);
		harness.SignInManager.CheckPasswordSignInAsync(user, "password", true).Returns(SignInResult.LockedOut);
		harness.UserManager.GetLockoutEndDateAsync(user).Returns(lockoutEnd);
		var sut = harness.CreateSut();

		var result = await sut.LoginAsync("user", "password", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.LockedOut, result.Error!.Type);
		var lockedOutError = Assert.IsType<LockedOutError>(result.Error);
		Assert.Equal(lockoutEnd, lockedOutError.RetryAfterUtc);
	}

	[Fact]
	public async Task LoginAsync_LockedOutWithoutKnownEnd_ReturnsLockedOutWithoutRetryAfter()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByNameAsync("user").Returns(user);
		harness.SignInManager.CheckPasswordSignInAsync(user, "password", true).Returns(SignInResult.LockedOut);
		harness.UserManager.GetLockoutEndDateAsync(user).Returns((DateTimeOffset?)null);
		var sut = harness.CreateSut();

		var result = await sut.LoginAsync("user", "password", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.LockedOut, result.Error!.Type);
		Assert.IsNotType<LockedOutError>(result.Error);
	}

	[Fact]
	public async Task LoginAsync_ValidCredentials_ReturnsTokensAndPersistsRefreshToken()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		var tokenId = Guid.NewGuid();
		var accessTokenExpiry = harness.TimeProvider.GetUtcNow().AddMinutes(15);
		harness.UserManager.FindByNameAsync("user").Returns(user);
		harness.SignInManager.CheckPasswordSignInAsync(user, "password", true).Returns(SignInResult.Success);
		harness.GuidProvider.CreateVersion7().Returns(tokenId);
		harness.JwtTokenGenerator.GenerateAccessToken(user).Returns(("access-token", accessTokenExpiry));
		var sut = harness.CreateSut();

		var result = await sut.LoginAsync("user", "password", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("access-token", result.Value.AccessToken);
		Assert.Equal(accessTokenExpiry, result.Value.AccessTokenExpiresAtUtc);
		Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
		_ = harness.RefreshTokenRepository.Received(1).AddAsync(
			Arg.Is<RefreshToken>(t => t != null && t.Id == tokenId && t.UserId == user.Id),
			Arg.Any<CancellationToken>());
		_ = harness.RefreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
	}
}
