using Auth.Core.Domain;
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
		harness.UserManager.CheckPasswordAsync(user, "WrongPass1").Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			user.Id, "WrongPass1", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.User.InvalidCurrentPassword", result.Error.Code);
	}

	[Fact]
	public async Task ChangePasswordAsync_NewPasswordMatchesCurrent_ReturnsValidationError()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.CheckPasswordAsync(user, "SamePass1").Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			user.Id, "SamePass1", "SamePass1", "SamePass1", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.NewPasswordMatchesCurrent", result.Error.Code);
	}

	[Fact]
	public async Task ChangePasswordAsync_WeakNewPassword_ReturnsMappedErrorWithoutStoringPendingChange()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.CheckPasswordAsync(user, "OldPass1").Returns(true);
		harness.PasswordValidator.ValidateAsync(harness.UserManager, user, "short")
			.Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			user.Id, "OldPass1", "short", "short", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
		_ = harness.PendingPasswordChangeRepository.DidNotReceive()
			.AddAsync(Arg.Any<PendingPasswordChange>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ChangePasswordAsync_ValidInput_StoresPendingChangeAndSendsCode()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.CheckPasswordAsync(user, "OldPass1").Returns(true);
		harness.PasswordValidator.ValidateAsync(harness.UserManager, user, "P@ssw0rd!").Returns(IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			user.Id, "OldPass1", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("user@test.com", result.Value.Email);
		Assert.Equal(
			harness.TimeProvider.GetUtcNow().Add(harness.PasswordChangeOptions.CodeLifetime),
			result.Value.CodeExpiresAtUtc);
		_ = harness.PendingPasswordChangeRepository.Received(1)
			.AddAsync(Arg.Any<PendingPasswordChange>(), Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordChangeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
		_ = harness.EmailSender.Received(1).SendPasswordChangeCodeAsync(
			"user@test.com", Arg.Any<string>(), harness.PasswordChangeOptions.CodeLifetime, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ChangePasswordAsync_ExistingPendingChangeForSameUser_ReplacesIt()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.CheckPasswordAsync(user, "OldPass1").Returns(true);
		harness.PasswordValidator.ValidateAsync(harness.UserManager, user, "P@ssw0rd!").Returns(IdentityResult.Success);
		var now = harness.TimeProvider.GetUtcNow();
		var existing = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("OLDCOD"), now.AddMinutes(-1), now.AddMinutes(14));
		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.ChangePasswordAsync(
			user.Id, "OldPass1", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.PendingPasswordChangeRepository.Received(1).RemoveAsync(existing, Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordChangeRepository.Received(1)
			.AddAsync(Arg.Any<PendingPasswordChange>(), Arg.Any<CancellationToken>());
	}
}
