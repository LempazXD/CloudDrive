using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Xunit;

namespace Auth.Infrastructure.Tests;

public sealed class LogoutAsyncTests
{
	[Fact]
	public async Task LogoutAsync_EmptyToken_ReturnsSuccessWithoutRepositoryCall()
	{
		var harness = new AuthServiceTestHarness();
		var sut = harness.CreateSut();

		var result = await sut.LogoutAsync("", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.RefreshTokenRepository.DidNotReceive()
			.TryRevokeByHashAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task LogoutAsync_ValidToken_RevokesAndReturnsSuccess()
	{
		var harness = new AuthServiceTestHarness();
		var expectedHash = RefreshTokenGenerator.Hash("raw-token");
		var sut = harness.CreateSut();

		var result = await sut.LogoutAsync("raw-token", CancellationToken.None);

		Assert.True(result.IsSuccess);
		_ = harness.RefreshTokenRepository.Received(1).TryRevokeByHashAsync(
			expectedHash, harness.TimeProvider.GetUtcNow(), Arg.Any<CancellationToken>());
	}
}
