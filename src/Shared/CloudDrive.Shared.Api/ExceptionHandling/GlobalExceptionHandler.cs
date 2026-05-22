using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CloudDrive.Shared.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(
	IProblemDetailsService problemDetailsService,
	ILogger<GlobalExceptionHandler> logger)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		if (exception is OperationCanceledException
		    && cancellationToken.IsCancellationRequested)
			return false;
		
		var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

		logger.LogError(
			exception,
			"Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
			httpContext.Request.Method,
			httpContext.Request.Path,
			traceId);

		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

		return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
		{
			HttpContext = httpContext,
			ProblemDetails = new ProblemDetails
			{
				Status = StatusCodes.Status500InternalServerError,
				Title = "Internal server error",
				Detail = "An unexpected error occurred",
				// TODO: Заменить на локализованную ошибку для пользователя
			}
		});
	}
}