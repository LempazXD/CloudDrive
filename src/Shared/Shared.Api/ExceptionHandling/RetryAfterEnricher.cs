using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Shared.Api.ExceptionHandling;

public static class RetryAfterEnricher
{
	public const string RetryAfterUtcKey = "retryAfterUtc";

	public static void Enrich(ProblemDetailsContext context)
	{
		if (!context.ProblemDetails.Extensions.TryGetValue(RetryAfterUtcKey, out var raw)
		    || raw is not DateTimeOffset retryAfterUtc)
			return;

		context.ProblemDetails.Extensions.Remove(RetryAfterUtcKey);

		var timeProvider = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
		var remaining = retryAfterUtc - timeProvider.GetUtcNow();

		// Блокировка уже истекла — заголовок не нужен.
		if (remaining <= TimeSpan.Zero)
			return;

		// Clamp до приведения к int: double -> int при переполнении даёт unspecified-значение по спецификации C#.
		var delaySeconds = Math.Min(remaining.TotalSeconds, int.MaxValue);

		context.HttpContext.Response.Headers[HeaderNames.RetryAfter] =
			((int)Math.Ceiling(delaySeconds)).ToString(CultureInfo.InvariantCulture);
	}
}
