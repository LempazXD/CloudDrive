using System.Security.Cryptography;
using System.Text;

namespace Auth.Infrastructure.Identity;

internal static class VerificationCodeGenerator
{
	private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

	public static string GenerateRaw() => RandomNumberGenerator.GetString(Alphabet, 6);

	public static string Hash(string rawCode) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)));
}
