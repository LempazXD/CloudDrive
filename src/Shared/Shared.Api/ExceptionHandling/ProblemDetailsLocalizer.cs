using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api.Localization;

namespace Shared.Api.ExceptionHandling;

public static class ProblemDetailsLocalizer
{
	public const string ErrorCodeKey = "errorCode";
	public const string TitleCodeKey = "titleCode";
	public const string ValidationFailuresKey = "validationFailures";
	private const string ErrorsKey = "errors";

	public static void Localize(ProblemDetailsContext context)
	{
		var extensions = context.ProblemDetails.Extensions;

		var hasErrorCode = extensions.TryGetValue(ErrorCodeKey, out var errorCodeRaw)
		                   && errorCodeRaw is string;
		var hasTitleCode = extensions.TryGetValue(TitleCodeKey, out var titleCodeRaw)
		                   && titleCodeRaw is string;
		var hasValidationFailures = extensions.TryGetValue(ValidationFailuresKey, out var failuresRaw)
		                            && failuresRaw is Dictionary<string, string[]>;

		if (!hasErrorCode && !hasTitleCode && !hasValidationFailures)
			return;

		var localizer = context.HttpContext.RequestServices.GetRequiredService<IErrorLocalizer>();
		var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

		if (hasErrorCode)
		{
			extensions.Remove(ErrorCodeKey);
			context.ProblemDetails.Detail = localizer.Localize((string)errorCodeRaw!, culture);
		}

		if (hasTitleCode)
		{
			extensions.Remove(TitleCodeKey);
			context.ProblemDetails.Title = localizer.Localize((string)titleCodeRaw!, culture);
		}

		if (hasValidationFailures)
		{
			extensions.Remove(ValidationFailuresKey);
			var failures = (Dictionary<string, string[]>)failuresRaw!;
			extensions[ErrorsKey] = failures.ToDictionary(
				failure => failure.Key,
				failure => failure.Value.Select(code => localizer.Localize(code, culture)).ToArray());
		}
	}
}
