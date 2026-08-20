using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class ForgotPasswordAsyncTests
{
	[Fact]
	public async Task ForgotPasswordAsync_EmptyEmail_ReturnsValidationError()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ForgotPasswordAsync("", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.InvalidEmail", result.Error.Code);
	}

	[Fact]
	public async Task ForgotPasswordAsync_UnknownEmail_ReturnsGenericSuccessWithoutSendingEmail()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.ForgotPasswordAsync("nobody@test.com", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("nobody@test.com", result.Value.Email);
		_ = harness.PendingPasswordResetRepository.DidNotReceive()
			.AddAsync(Arg.Any<PendingPasswordReset>(), Arg.Any<CancellationToken>());
		_ = harness.EmailSender.DidNotReceive().SendPasswordResetCodeAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ForgotPasswordAsync_KnownEmail_StoresPendingResetAndSendsCode()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var sut = harness.CreateSut();

		var result = await sut.ForgotPasswordAsync("user@test.com", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("user@test.com", result.Value.Email);
		Assert.Equal(
			harness.TimeProvider.GetUtcNow().Add(harness.PasswordResetOptions.CodeLifetime),
			result.Value.CodeExpiresAtUtc);
		_ = harness.PendingPasswordResetRepository.Received(1)
			.AddAsync(Arg.Any<PendingPasswordReset>(), Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordResetRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
		_ = harness.EmailSender.Received(1).SendPasswordResetCodeAsync(
			"user@test.com", Arg.Any<string>(), harness.PasswordResetOptions.CodeLifetime, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ForgotPasswordAsync_ExistingPendingResetForSameUser_ReplacesIt()
	{
		var harness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		harness.UserManager.FindByEmailAsync("user@test.com").Returns(user);
		var now = harness.TimeProvider.GetUtcNow();
		var existing = PendingPasswordReset.Create(
			Guid.NewGuid(), user.Id, VerificationCodeGenerator.Hash("OLDCOD"), now.AddMinutes(-1), now.AddMinutes(14));
		harness.PendingPasswordResetRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.ForgotPasswordAsync("user@test.com", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.PendingPasswordResetRepository.Received(1).RemoveAsync(existing, Arg.Any<CancellationToken>());
		_ = harness.PendingPasswordResetRepository.Received(1)
			.AddAsync(Arg.Any<PendingPasswordReset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ForgotPasswordAsync_UnknownAndKnownEmail_ReturnIndistinguishableResponseShape()
	{
		var unknownHarness = new AuthServiceTestHarness();
		var unknownResult = await unknownHarness.CreateSut().ForgotPasswordAsync("nobody@test.com", CancellationToken.None);

		var knownHarness = new AuthServiceTestHarness();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "somebody@test.com" };
		knownHarness.UserManager.FindByEmailAsync("somebody@test.com").Returns(user);
		var knownResult = await knownHarness.CreateSut().ForgotPasswordAsync("somebody@test.com", CancellationToken.None);

		Assert.True(unknownResult.IsSuccess);
		Assert.True(knownResult.IsSuccess);
		// Оба харнесса стартуют с одного и того же замороженного времени и CodeLifetime, поэтому
		// одинаковый CodeExpiresAtUtc подтверждает, что по форме ответа нельзя отличить
		// "аккаунт существует" от "не существует".
		Assert.Equal(unknownResult.Value.CodeExpiresAtUtc, knownResult.Value.CodeExpiresAtUtc);
	}
}
