using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class ConfirmChangePasswordAsyncTests
{
	[Fact]
	public async Task ConfirmChangePasswordAsync_EmptyCode_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			Guid.NewGuid(), "", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.PasswordChange.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_UserNotFound_ReturnsUserNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var userId = Guid.NewGuid();
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			userId, "ABC123", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.User.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_NoPendingChange_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			user.Id, "ABC123", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.PasswordChange.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_ExpiredCode_DeletesRowAndReturnsExpired()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now.AddMinutes(-20), now.AddMinutes(-5));
		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			user.Id, "ABC123", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.PasswordChange.Expired", result.Error.Code);
		_ = harness.PendingPasswordChangeRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_WrongCode_RecordsAttemptAndReturnsInvalid()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			user.Id, "WRONG1", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.PasswordChange.Invalid", result.Error.Code);
		Assert.Equal(1, pending.AttemptCount);
		_ = harness.PendingPasswordChangeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordChangeRepository.DidNotReceive()
			.RemoveAsync(Arg.Any<PendingPasswordChange>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_ExceedsMaxAttempts_DeletesRowAndReturnsTooManyAttempts()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		for (var i = 0; i < harness.PasswordChangeOptions.MaxAttempts - 1; i++)
			pending.RecordFailedAttempt();

		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			user.Id, "WRONG1", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.PasswordChange.TooManyAttempts", result.Error.Code);
		_ = harness.PendingPasswordChangeRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_CorrectCodeButPasswordsDoNotMatch_ReturnsValidationErrorWithoutRecordingAttempt()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			user.Id, "ABC123", "P@ssw0rd", "Different1", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.PasswordConfirmationMismatch", result.Error.Code);
		// Код был верным - неверное подтверждение пароля не должно тратить AttemptCount.
		Assert.Equal(0, pending.AttemptCount);
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_CorrectCodeButEmptyNewPassword_ReturnsWeakPassword()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(user.Id, "ABC123", "", "", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_IdentityResetFails_ReturnsMappedError()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		harness.UserManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");
		harness.UserManager.ResetPasswordAsync(user, "reset-token", "short")
			.Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(user.Id, "ABC123", "short", "short", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
		_ = harness.PendingPasswordChangeRepository.DidNotReceive()
			.RemoveAsync(Arg.Any<PendingPasswordChange>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_CorrectCodeAndValidPassword_CompletesChangeRevokesSessionsAndIssuesTokens()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		harness.UserManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");
		harness.UserManager.ResetPasswordAsync(user, "reset-token", "P@ssw0rd!").Returns(IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			user.Id, "ABC123", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.PendingPasswordChangeRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordChangeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
		_ = harness.UserManager.Received(1).ResetAccessFailedCountAsync(user);
		_ = harness.UserManager.Received(1).SetLockoutEndDateAsync(user, null);
		_ = harness.RefreshTokenRepository.Received(1)
			.RevokeAllForUserAsync(user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmChangePasswordAsync_Success_AlsoRemovesPendingPasswordResetForSameUser()
	{
		// Не даёт протухшей заявке /forgot-password пережить смену пароля через /change-password -
		// иначе её код остаётся годным до своего истечения и позволяет установить пароль без
		// повторной проверки текущего (который к этому моменту уже сменился).
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordChange.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordChangeRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		harness.UserManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");
		harness.UserManager.ResetPasswordAsync(user, "reset-token", "P@ssw0rd!").Returns(IdentityResult.Success);
		var staleReset = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("STALE1"), now.AddMinutes(-10), now.AddMinutes(5));
		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(staleReset);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmChangePasswordAsync(
			user.Id, "ABC123", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.PendingPasswordResetRepository.Received(1).RemoveAsync(staleReset, Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordResetRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
	}
}
