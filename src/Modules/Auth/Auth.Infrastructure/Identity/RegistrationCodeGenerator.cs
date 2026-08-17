using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Auth.Infrastructure.Identity;

internal static class RegistrationCodeGenerator
{
	public static string GenerateRaw() =>
		RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

	public static string Hash(string rawCode) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)));
}
