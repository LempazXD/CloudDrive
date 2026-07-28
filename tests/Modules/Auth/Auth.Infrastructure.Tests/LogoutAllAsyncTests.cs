using Auth.Core.Domain;
using Auth.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Shared.Kernel.Results;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class LogoutAllAsyncTests
{
	[Fact]
	public async Task LogoutAllAsync_EmptyToken_ReturnsUnauthorized()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.LogoutAllAsync("", keepCurrentSession: false, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.RefreshToken.Invalid", result.Error.Code);
		_ = harness.RefreshTokenRepository.DidNotReceive()
			.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task LogoutAllAsync_UnknownTokenHash_ReturnsUnauthorized()
	{
		var harness = new AuthServiceTestHarness();
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns((RefreshToken?)null);
		var sut = harness.CreateSut();

		var result = await sut.LogoutAllAsync("raw-token", keepCurrentSession: false, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.RefreshToken.Invalid", result.Error!.Code);
	}

	[Fact]
	public async Task LogoutAllAsync_RevokedToken_ReturnsRevoked()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var existing = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddDays(-1), now.AddDays(29))
			.SetRevoked(now.AddMinutes(-1));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.LogoutAllAsync("raw-token", keepCurrentSession: false, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
		Assert.Equal("Auth.RefreshToken.Revoked", result.Error.Code);
		_ = harness.RefreshTokenRepository.DidNotReceive()
			.RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task LogoutAllAsync_ExpiredToken_ReturnsExpired()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var existing = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddDays(-31), now.AddMinutes(-1));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.LogoutAllAsync("raw-token", keepCurrentSession: false, CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal("Auth.RefreshToken.Expired", result.Error!.Code);
	}

	[Fact]
	public async Task LogoutAllAsync_KeepCurrentSessionFalse_RevokesAllForUser()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var userId = Guid.NewGuid();
		var sessionId = Guid.NewGuid();
		var existing = RefreshToken.Create(Guid.NewGuid(), userId, sessionId, "hash", now.AddDays(-1), now.AddDays(29));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.LogoutAllAsync("raw-token", keepCurrentSession: false, CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.RefreshTokenRepository.Received(1)
			.RevokeAllForUserAsync(userId, now, Arg.Any<CancellationToken>());
		_ = harness.RefreshTokenRepository.DidNotReceive().RevokeAllForUserExceptSessionAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task LogoutAllAsync_KeepCurrentSessionTrue_RevokesAllExceptCurrentSession()
	{
		var harness = new AuthServiceTestHarness();
		var now = harness.TimeProvider.GetUtcNow();
		var userId = Guid.NewGuid();
		var sessionId = Guid.NewGuid();
		var existing = RefreshToken.Create(Guid.NewGuid(), userId, sessionId, "hash", now.AddDays(-1), now.AddDays(29));
		harness.RefreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(existing);
		var sut = harness.CreateSut();

		var result = await sut.LogoutAllAsync("raw-token", keepCurrentSession: true, CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.RefreshTokenRepository.Received(1)
			.RevokeAllForUserExceptSessionAsync(userId, sessionId, now, Arg.Any<CancellationToken>());
		_ = harness.RefreshTokenRepository.DidNotReceive()
			.RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}
}
