using System.Reflection;
using Auth.Core.Domain;

namespace Auth.Infrastructure.Tests.TestSupport;

// RevokedAtUtc выставляется напрямую: ExecuteUpdateAsync в репозитории или
// private-setter при материализации EF Core. Reflection здесь повторяет тот же
// путь, а не обходит инкапсуляцию.
internal static class RefreshTokenTestExtensions
{
	private static readonly PropertyInfo RevokedAtUtcProperty =
		typeof(RefreshToken).GetProperty(nameof(RefreshToken.RevokedAtUtc))!;

	public static RefreshToken SetRevoked(this RefreshToken token, DateTimeOffset revokedAtUtc)
	{
		RevokedAtUtcProperty.SetValue(token, revokedAtUtc);
		return token;
	}
}
