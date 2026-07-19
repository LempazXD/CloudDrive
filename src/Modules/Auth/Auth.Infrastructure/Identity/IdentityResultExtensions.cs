using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Extensions;
using Shared.Kernel.Results;

namespace Auth.Infrastructure.Identity;

internal static class IdentityResultExtensions
{
	public static Result ToResult(this IdentityResult result, string fallbackErrorCode, ILogger logger) =>
		result.Succeeded ? Result.Success() : Result.Failure(ToError(result, fallbackErrorCode, logger));

	public static Result<T> ToResult<T>(this IdentityResult result, T value, string fallbackErrorCode, ILogger logger) =>
		result.ToResult(fallbackErrorCode, logger).ToValueResult(value);

	private static Error ToError(IdentityResult result, string fallbackErrorCode)
	{
		foreach (var error in result.Errors)
		{
			var mapped = MapKnownError(error.Code);
			if (mapped is not null)
				return mapped;
		}

		logger.LogWarning(
			"IdentityResult had no known mapping for codes [{ErrorCodes}]; falling back to {FallbackErrorCode}.",
			string.Join(',', result.Errors.Select(e => e.Code)), fallbackErrorCode);
		return Error.Validation(fallbackErrorCode);
	}

	private static Error? MapKnownError(string code) =>
		code switch
		{
			"DuplicateUserName" => Error.Conflict("Auth.User.UsernameAlreadyExists"),
			"DuplicateEmail" => Error.Conflict("Auth.User.EmailAlreadyExists"),
			"PasswordTooShort" or "PasswordRequiresNonAlphanumeric" or "PasswordRequiresDigit"
				or "PasswordRequiresUpper" or "PasswordRequiresLower" or "PasswordRequiresUniqueChars"
				=> Error.Validation("Auth.User.WeakPassword"),
			"InvalidEmail" => Error.Validation("Auth.User.InvalidEmail"),
			"ConcurrencyFailure" => Error.Conflict("Auth.User.ConcurrencyConflict"),
			_ => null
		};
}
