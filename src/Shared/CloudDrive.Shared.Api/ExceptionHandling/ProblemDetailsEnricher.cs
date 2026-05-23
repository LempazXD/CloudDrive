using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace CloudDrive.Shared.Api.ExceptionHandling;

public static class ProblemDetailsEnricher
{
	public static void Enrich(ProblemDetailsContext context)
	{
		var httpContext = context.HttpContext;

		context.ProblemDetails.Type = null;

		context.ProblemDetails.Instance = httpContext.Request.Path;

		context.ProblemDetails.Extensions["method"] = httpContext.Request.Method;

		context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString();

		context.ProblemDetails.Extensions["requestId"] = httpContext.TraceIdentifier;

		context.ProblemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow.ToString("O");
	}
}