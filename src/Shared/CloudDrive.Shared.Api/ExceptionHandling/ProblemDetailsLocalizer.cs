using System.Globalization;
using CloudDrive.Shared.Api.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CloudDrive.Shared.Api.ExceptionHandling;

public static class ProblemDetailsLocalizer
{
	public const string ErrorCodeKey = "errorCode";

	public static void Localize(ProblemDetailsContext context)
	{
		if (!context.ProblemDetails.Extensions.TryGetValue(ErrorCodeKey, out var raw)
		    || raw is not string errorCode)
			return;

		var localizer = context.HttpContext.RequestServices.GetRequiredService<IErrorLocalizer>();
		var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

		context.ProblemDetails.Detail = localizer.Localize(errorCode, culture);
	}
}