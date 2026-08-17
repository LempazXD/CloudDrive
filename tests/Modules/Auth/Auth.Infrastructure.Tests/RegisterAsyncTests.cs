using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class RegisterAsyncTests
{
	[Fact]
	public async Task RegisterAsync_EmptyUsername_ReturnsValidationError()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("", "user@test.com", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.InvalidUsername", result.Error.Code);
	}

	[Fact]
	public async Task RegisterAsync_EmptyEmail_ReturnsValidationError()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.InvalidEmail", result.Error.Code);
	}

	[Fact]
	public async Task RegisterAsync_EmptyPassword_ReturnsValidationError()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
	}

	[Fact]
	public async Task RegisterAsync_ValidInput_StoresPendingRegistrationAndSendsCode()
	{
		var harness = new AuthServiceTestHarness();
		StubValidators(harness, userResult: IdentityResult.Success, passwordResult: IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("user@test.com", result.Value.Email);
		Assert.Equal(
			harness.TimeProvider.GetUtcNow().Add(harness.RegistrationOptions.CodeLifetime),
			result.Value.CodeExpiresAtUtc);
		_ = harness.PendingRegistrationRepository.Received(1)
			.AddAsync(Arg.Any<PendingRegistration>(), Arg.Any<CancellationToken>());
		_ = harness.PendingRegistrationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
		_ = harness.EmailSender.Received(1).SendRegistrationCodeAsync(
			"user@test.com", Arg.Any<string>(), harness.RegistrationOptions.CodeLifetime, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RegisterAsync_ExistingPendingRegistrationForSameEmail_ReplacesIt()
	{
		var harness = new AuthServiceTestHarness();
		StubValidators(harness, userResult: IdentityResult.Success, passwordResult: IdentityResult.Success);
		var now = harness.TimeProvider.GetUtcNow();
		var existing = PendingRegistration.Create(
			Guid.NewGuid(), "USER@TEST.COM", "user@test.com", "olduser",
			"old-hash", "old-code-hash", now.AddMinutes(-1), now.AddMinutes(14));
		harness.PendingRegistrationRepository.GetByNormalizedEmailAsync("USER@TEST.COM", Arg.Any<CancellationToken>())
			.Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.PendingRegistrationRepository.Received(1).RemoveAsync(existing, Arg.Any<CancellationToken>());
		_ = harness.PendingRegistrationRepository.Received(1)
			.AddAsync(Arg.Any<PendingRegistration>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RegisterAsync_DuplicateUsername_ReturnsConflict()
	{
		var harness = new AuthServiceTestHarness();
		StubValidators(
			harness,
			userResult: IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName" }),
			passwordResult: IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Conflict, result.Error!.Type);
		Assert.Equal("Auth.User.UsernameAlreadyExists", result.Error.Code);
	}

	[Fact]
	public async Task RegisterAsync_DuplicateEmail_ReturnsConflict()
	{
		var harness = new AuthServiceTestHarness();
		StubValidators(
			harness,
			userResult: IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail" }),
			passwordResult: IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Conflict, result.Error!.Type);
		Assert.Equal("Auth.User.EmailAlreadyExists", result.Error.Code);
	}

	[Fact]
	public async Task RegisterAsync_WeakPassword_ReturnsValidationError()
	{
		var harness = new AuthServiceTestHarness();
		StubValidators(
			harness,
			userResult: IdentityResult.Success,
			passwordResult: IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "short", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.WeakPassword", result.Error.Code);
	}

	[Fact]
	public async Task RegisterAsync_UnrecognizedIdentityError_ReturnsFallbackValidationError()
	{
		var harness = new AuthServiceTestHarness();
		StubValidators(
			harness,
			userResult: IdentityResult.Failed(new IdentityError { Code = "SomeFutureIdentityCode" }),
			passwordResult: IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.RegistrationFailed", result.Error.Code);
	}

	private static void StubValidators(AuthServiceTestHarness harness, IdentityResult userResult, IdentityResult passwordResult)
	{
		harness.UserValidator.ValidateAsync(harness.UserManager, Arg.Any<ApplicationUser>()).Returns(userResult);
		harness.PasswordValidator.ValidateAsync(harness.UserManager, Arg.Any<ApplicationUser>(), Arg.Any<string>())
			.Returns(passwordResult);
	}
}
