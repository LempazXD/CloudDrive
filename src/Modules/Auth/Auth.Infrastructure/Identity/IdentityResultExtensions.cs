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

	// Просматривает все IdentityError, а не только первую, поэтому распознанная ошибка, идущая
	// после нераспознанной (или при пустой коллекции Errors), никогда не проскочит незамеченной.
	// TODO: тем не менее возвращается только первая распознанная ошибка - если пароль нарушает сразу
	// несколько правил (например, PasswordTooShort и PasswordRequiresDigit одновременно), клиент узнаёт
	// только про одно из них за запрос и должен будет присылать повторные попытки, чтобы обнаружить
	// остальные. Правильный вариант - собирать все распознанные ошибки в список ValidationFailure
	// (PropertyName, ReasonCode) и возвращать один Error.Validation(...) через ValidationError вместо
	// первого найденного совпадения.

	private static Error ToError(IdentityResult result, string fallbackErrorCode, ILogger logger)
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
