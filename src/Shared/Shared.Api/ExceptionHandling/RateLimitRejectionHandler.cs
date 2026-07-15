using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Api.ExceptionHandling;

public static class RateLimitRejectionHandler
{
	public static async ValueTask HandleAsync(OnRejectedContext context, CancellationToken ct)
	{
		var timeProvider = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
		var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

		var problemDetails = new ProblemDetails
		{
			Status = StatusCodes.Status429TooManyRequests,
			Extensions =
			{
				[ProblemDetailsLocalizer.ErrorCodeKey] = "Http.RateLimit.Exceeded",
				[ProblemDetailsLocalizer.TitleCodeKey] = "Http.Title.TooManyRequests"
			}
		};

		if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
			problemDetails.Extensions[RetryAfterEnricher.RetryAfterUtcKey] = timeProvider.GetUtcNow().Add(retryAfter);

		await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
		{
			HttpContext = context.HttpContext,
			ProblemDetails = problemDetails
		});
	}
}
