using Auth.Core.Application.Abstractions;
using Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure.Caching;

internal sealed class RefreshTokenReplayCache(IMemoryCache cache, IOptions<JwtOptions> jwtOptions) : IRefreshTokenReplayCache
{
	public void Set(string consumedTokenHash, AuthTokens tokens)
	{
		var gracePeriod = jwtOptions.Value.RefreshTokenReuseGracePeriod;
		if (gracePeriod <= TimeSpan.Zero)
			return;

		cache.Set(CacheKey(consumedTokenHash), tokens, gracePeriod);
	}

	public bool TryGet(string consumedTokenHash, out AuthTokens tokens)
	{
		if (cache.TryGetValue<AuthTokens>(CacheKey(consumedTokenHash), out var cached) && cached is not null)
		{
			tokens = cached;
			return true;
		}

		tokens = null!;
		return false;
	}

	private static string CacheKey(string consumedTokenHash) => $"refresh-replay:{consumedTokenHash}";
}
