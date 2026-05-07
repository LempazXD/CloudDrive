using CloudDrive.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CloudDrive.Common.Api.Extensions;

public static class AppErrorExtensions
{
	extension(Error error)
	{
		public ProblemDetails ToProblemDetails()
		{
			var status = error.Type.ToHttpStatusCode();

			return new()
			{
				Status = status,
				Title = error.Type.ToTitle(),
				Detail = status == StatusCodes.Status500InternalServerError  // Не раскрываем внутренние детали клиенту
					? "An unexpected error occurred"
					: error.Description,
				Extensions = { ["code"] = error.Code }
			};
		}
	}
}
