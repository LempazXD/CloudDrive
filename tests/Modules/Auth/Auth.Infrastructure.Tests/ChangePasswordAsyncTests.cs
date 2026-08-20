using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class ChangePasswordAsyncTests
{
	[Fact]
	public async Task ChangePasswordAsync_EmptyNewPassword_ReturnsWeakPassword()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			Guid.NewGuid(), "OldPass1", "", "", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
		_ = harness.UserManager.DidNotReceive().FindByIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task ChangePasswordAsync_PasswordsDoNotMatch_ReturnsValidationError()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			Guid.NewGuid(), "OldPass1", "P@ssw0rd!", "Different1", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.PasswordConfirmationMismatch", result.Error.Code);
		_ = harness.UserManager.DidNotReceive().FindByIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task ChangePasswordAsync_UserNotFound_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var userId = Guid.NewGuid();
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			userId, "OldPass1", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.User.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsInvalidCurrentPassword()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.ChangePasswordAsync(user, "WrongPass1", "P@ssw0rd!")
			.Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch" }));
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			user.Id, "WrongPass1", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.User.InvalidCurrentPassword", result.Error.Code);
	}

	[Fact]
	public async Task ChangePasswordAsync_IdentityChangeFails_ReturnsMappedFallbackError()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.ChangePasswordAsync(user, "OldPass1", "short")
			.Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			user.Id, "OldPass1", "short", "short", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
	}

	[Fact]
	public async Task ChangePasswordAsync_ValidInput_ChangesPasswordRevokesSessionsAndIssuesTokens()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.ChangePasswordAsync(user, "OldPass1", "P@ssw0rd!").Returns(IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			user.Id, "OldPass1", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.UserManager.Received(1).ResetAccessFailedCountAsync(user);
		_ = harness.UserManager.Received(1).SetLockoutEndDateAsync(user, null);
		_ = harness.RefreshTokenRepository.Received(1)
			.RevokeAllForUserAsync(user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}
}
