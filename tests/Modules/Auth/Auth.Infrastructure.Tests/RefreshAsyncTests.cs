using Auth.Core.Domain;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class RefreshAsyncTests
{
	[Fact]
	public async Task RefreshAsync_EmptyToken_ReturnsInvalid()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.RefreshAsync("", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.RefreshToken.Invalid", result.Error.Code);
		_ = harness.RefreshTokenRepository.DidNotReceive()
			.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RefreshAsync_UnknownTokenHash_ReturnsInvalid()
	{
		var harness = new AuthServiceTestHarness();
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns((RefreshToken?)null);
		var sut = harness.CreateSut();

		var result = await sut.RefreshAsync("raw-token", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.RefreshToken.Invalid", result.Error!.Code);
	}

	[Fact]
	public async Task RefreshAsync_RevokedToken_RevokesAllAndReturnsRevoked()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var existing = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddDays(-1), now.AddDays(29))
			.SetRevoked(now.AddMinutes(-1));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.RefreshAsync("raw-token", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.RefreshToken.Revoked", result.Error.Code);
		_ = harness.RefreshTokenRepository.Received(1)
			.RevokeAllForUserAsync(existing.UserId, now, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RefreshAsync_ExpiredToken_ReturnsExpired()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var existing = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddDays(-31), now.AddMinutes(-1));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.RefreshAsync("raw-token", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.RefreshToken.Expired", result.Error!.Code);
	}

	[Fact]
	public async Task RefreshAsync_UserNotFound_ReturnsInvalid()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var existing = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddDays(-1), now.AddDays(29));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		harness.UserManager.FindByIdAsync(existing.UserId.ToString()).Returns((ApplicationUser?)null);
		var sut = harness.CreateSut();

		var result = await sut.RefreshAsync("raw-token", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.RefreshToken.Invalid", result.Error!.Code);
	}

	[Fact]
	public async Task RefreshAsync_UserLockedOut_ReturnsLockedOut()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		var existing = RefreshToken.Create(Guid.NewGuid(), user.Id, "hash", now.AddDays(-1), now.AddDays(29));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.IsLockedOutAsync(user).Returns(true);
		harness.UserManager.GetLockoutEndDateAsync(user).Returns(now.AddMinutes(5));
		var sut = harness.CreateSut();

		var result = await sut.RefreshAsync("raw-token", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.LockedOut, result.Error!.Type);
	}

	[Fact]
	public async Task RefreshAsync_ValidToken_RotatesAndReturnsNewTokens()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		var existing = RefreshToken.Create(Guid.NewGuid(), user.Id, "hash", now.AddDays(-1), now.AddDays(29));
		var newTokenId = Guid.NewGuid();
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.IsLockedOutAsync(user).Returns(false);
		harness.GuidProvider.CreateVersion7().Returns(newTokenId);
		harness.JwtTokenGenerator.GenerateAccessToken(user).Returns(("access-token", now.AddMinutes(15)));
		harness.RefreshTokenRepository
			.TryRotateAsync(existing.Id, Arg.Any<RefreshToken>(), now, Arg.Any<CancellationToken>())
			.Returns(true);
		var sut = harness.CreateSut();

		var result = await sut.RefreshAsync("raw-token", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("access-token", result.Value.AccessToken);
	}

	[Fact]
	public async Task RefreshAsync_RotationRaceLost_ReturnsRevoked()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user", Email = "user@test.com" };
		var existing = RefreshToken.Create(Guid.NewGuid(), user.Id, "hash", now.AddDays(-1), now.AddDays(29));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		harness.UserManager.FindByIdAsync(user.Id.ToString()).Returns(user);
		harness.UserManager.IsLockedOutAsync(user).Returns(false);
		harness.RefreshTokenRepository
			.TryRotateAsync(existing.Id, Arg.Any<RefreshToken>(), now, Arg.Any<CancellationToken>())
			.Returns(false);
		var sut = harness.CreateSut();

		var result = await sut.RefreshAsync("raw-token", CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.RefreshToken.Revoked", result.Error!.Code);
	}
}
