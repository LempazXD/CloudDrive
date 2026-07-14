using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Auth.Infrastructure.Identity;

internal static class RefreshTokenGenerator
{
	public static string GenerateRaw() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));

	public static string Hash(string rawToken) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
