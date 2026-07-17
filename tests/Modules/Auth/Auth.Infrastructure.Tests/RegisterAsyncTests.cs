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
	public async Task RegisterAsync_ValidInput_ReturnsSuccessWithUserSummary()
	{
		var harness = new AuthServiceTestHarness();
		harness.UserManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
			.Returns(IdentityResult.Success);
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("user", result.Value.Username);
		Assert.Equal("user@test.com", result.Value.Email);
	}

	[Fact]
	public async Task RegisterAsync_DuplicateUsername_ReturnsConflict()
	{
		var harness = new AuthServiceTestHarness();
		harness.UserManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
			.Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName" }));
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
		harness.UserManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
			.Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail" }));
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
		harness.UserManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
			.Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));
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
		harness.UserManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
			.Returns(IdentityResult.Failed(new IdentityError { Code = "SomeFutureIdentityCode" }));
		var sut = harness.CreateSut();

		var result = await sut.RegisterAsync("user", "user@test.com", "P@ssw0rd", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error!.Type);
		Assert.Equal("Auth.User.RegistrationFailed", result.Error.Code);
	}
}
