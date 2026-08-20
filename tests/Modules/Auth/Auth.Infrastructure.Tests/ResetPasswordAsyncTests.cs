using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class ResetPasswordAsyncTests
{
	[Fact]
	public async Task ResetPasswordAsync_EmptyEmail_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("", "ABC123", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.PasswordReset.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ResetPasswordAsync_EmptyCode_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.PasswordReset.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ResetPasswordAsync_UnknownEmail_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("nobody@test.com", "ABC123", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.PasswordReset.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ResetPasswordAsync_NoPendingReset_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "ABC123", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.PasswordReset.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ResetPasswordAsync_ExpiredCode_DeletesRowAndReturnsExpired()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now.AddMinutes(-20), now.AddMinutes(-5));
		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "ABC123", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.PasswordReset.Expired", result.Error.Code);
		_ = harness.PendingPasswordResetRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ResetPasswordAsync_WrongCode_RecordsAttemptAndReturnsInvalid()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "WRONG1", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.PasswordReset.Invalid", result.Error.Code);
		Assert.Equal(1, pending.AttemptCount);
		_ = harness.PendingPasswordResetRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordResetRepository.DidNotReceive()
			.RemoveAsync(Arg.Any<PendingPasswordReset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ResetPasswordAsync_ExceedsMaxAttempts_DeletesRowAndReturnsTooManyAttempts()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		for (var i = 0; i < harness.PasswordResetOptions.MaxAttempts - 1; i++)
			pending.RecordFailedAttempt();

		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "WRONG1", "P@ssw0rd", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.PasswordReset.TooManyAttempts", result.Error.Code);
		_ = harness.PendingPasswordResetRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ResetPasswordAsync_CorrectCodeButPasswordsDoNotMatch_ReturnsValidationErrorWithoutRecordingAttempt()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "ABC123", "P@ssw0rd", "Different1", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.PasswordConfirmationMismatch", result.Error.Code);
		// Код был верным - неверное подтверждение пароля не должно тратить AttemptCount.
		Assert.Equal(0, pending.AttemptCount);
	}

	[Fact]
	public async Task ResetPasswordAsync_CorrectCodeButEmptyNewPassword_ReturnsWeakPassword()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "ABC123", "", "", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
	}

	[Fact]
	public async Task ResetPasswordAsync_IdentityResetFails_ReturnsMappedError()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		harness.UserManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");
		harness.UserManager.ResetPasswordAsync(user, "reset-token", "short")
			.Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "ABC123", "short", "short", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
		_ = harness.PendingPasswordResetRepository.DidNotReceive()
			.RemoveAsync(Arg.Any<PendingPasswordReset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ResetPasswordAsync_CorrectCodeAndValidPassword_CompletesResetRevokesSessionsAndIssuesTokens()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("ABC123"), now, now.AddMinutes(15));
		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(pending);
		harness.UserManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");
		harness.UserManager.ResetPasswordAsync(user, "reset-token", "P@ssw0rd!").Returns(IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.ResetPasswordAsync("user@test.com", "ABC123", "P@ssw0rd!", "P@ssw0rd!", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.PendingPasswordResetRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordResetRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
		_ = harness.UserManager.Received(1).ResetAccessFailedCountAsync(user);
		_ = harness.UserManager.Received(1).SetLockoutEndDateAsync(user, null);
		_ = harness.RefreshTokenRepository.Received(1)
			.RevokeAllForUserAsync(user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}
}
