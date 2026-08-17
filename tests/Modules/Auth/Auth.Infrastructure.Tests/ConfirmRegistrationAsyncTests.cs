using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class ConfirmRegistrationAsyncTests
{
	[Fact]
	public async Task ConfirmRegistrationAsync_EmptyEmail_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ConfirmRegistrationAsync("", "123456", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.RegistrationCode.NotFound", result.Error.Code);
		_ = harness.PendingRegistrationRepository.DidNotReceive()
			.GetByNormalizedEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmRegistrationAsync_NoPendingRegistration_ReturnsNotFound()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ConfirmRegistrationAsync("user@test.com", "123456", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error!.Type);
		Assert.Equal("Auth.RegistrationCode.NotFound", result.Error.Code);
	}

	[Fact]
	public async Task ConfirmRegistrationAsync_ExpiredCode_DeletesRowAndReturnsExpired()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingRegistration.Create(
			Guid.NewGuid(), "USER@TEST.COM", "user@test.com", "user",
			"password-hash", RegistrationCodeGenerator.Hash("123456"), now.AddMinutes(-20), now.AddMinutes(-5));
		harness.PendingRegistrationRepository.GetByNormalizedEmailAsync("USER@TEST.COM", Arg.Any<CancellationToken>())
			.Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmRegistrationAsync("user@test.com", "123456", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.RegistrationCode.Expired", result.Error.Code);
		_ = harness.PendingRegistrationRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmRegistrationAsync_WrongCode_RecordsAttemptAndReturnsInvalid()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingRegistration.Create(
			Guid.NewGuid(), "USER@TEST.COM", "user@test.com", "user",
			"password-hash", RegistrationCodeGenerator.Hash("123456"), now, now.AddMinutes(15));
		harness.PendingRegistrationRepository.GetByNormalizedEmailAsync("USER@TEST.COM", Arg.Any<CancellationToken>())
			.Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmRegistrationAsync("user@test.com", "000000", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.RegistrationCode.Invalid", result.Error.Code);
		Assert.Equal(1, pending.AttemptCount);
		_ = harness.PendingRegistrationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
		_ = harness.PendingRegistrationRepository.DidNotReceive()
			.RemoveAsync(Arg.Any<PendingRegistration>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmRegistrationAsync_ExceedsMaxAttempts_DeletesRowAndReturnsTooManyAttempts()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingRegistration.Create(
			Guid.NewGuid(), "USER@TEST.COM", "user@test.com", "user",
			"password-hash", RegistrationCodeGenerator.Hash("123456"), now, now.AddMinutes(15));
		for (var i = 0; i < harness.RegistrationOptions.MaxAttempts - 1; i++)
			pending.RecordFailedAttempt();

		harness.PendingRegistrationRepository.GetByNormalizedEmailAsync("USER@TEST.COM", Arg.Any<CancellationToken>())
			.Returns(pending);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmRegistrationAsync("user@test.com", "000000", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.RegistrationCode.TooManyAttempts", result.Error.Code);
		_ = harness.PendingRegistrationRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmRegistrationAsync_CorrectCode_CreatesUserIssuesTokensAndDeletesPendingRow()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingRegistration.Create(
			Guid.NewGuid(), "USER@TEST.COM", "user@test.com", "user",
			"password-hash", RegistrationCodeGenerator.Hash("123456"), now, now.AddMinutes(15));
		harness.PendingRegistrationRepository.GetByNormalizedEmailAsync("USER@TEST.COM", Arg.Any<CancellationToken>())
			.Returns(pending);
		harness.UserManager.CreateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.ConfirmRegistrationAsync("user@test.com", "123456", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.UserManager.Received(1).CreateAsync(Arg.Is<ApplicationUser>(u =>
			u != null && u.EmailConfirmed && u.PasswordHash == "password-hash" && u.UserName == "user" && u.Email == "user@test.com"));
		_ = harness.PendingRegistrationRepository.Received(1).RemoveAsync(pending, Arg.Any<CancellationToken>());
		_ = harness.PendingRegistrationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConfirmRegistrationAsync_CreateAsyncFails_ReturnsFailureWithoutRecordingAttemptOrDeletingRow()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var pending = PendingRegistration.Create(
			Guid.NewGuid(), "USER@TEST.COM", "user@test.com", "user",
			"password-hash", RegistrationCodeGenerator.Hash("123456"), now, now.AddMinutes(15));
		harness.PendingRegistrationRepository.GetByNormalizedEmailAsync("USER@TEST.COM", Arg.Any<CancellationToken>())
			.Returns(pending);
		harness.UserManager.CreateAsync(Arg.Any<ApplicationUser>())
			.Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName" }));
		var sut = harness.CreateSut();

		var result = await sut.ConfirmRegistrationAsync("user@test.com", "123456", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Conflict, result.Error!.Type);
		Assert.Equal("Auth.User.UsernameAlreadyExists", result.Error.Code);
		Assert.Equal(0, pending.AttemptCount);
		_ = harness.PendingRegistrationRepository.DidNotReceive()
			.RemoveAsync(Arg.Any<PendingRegistration>(), Arg.Any<CancellationToken>());
	}
}
