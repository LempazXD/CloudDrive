using CloudDrive.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CloudDrive.Common.Api.Extensions;

public static class AppErrorExtensions
{
	extension(AppError error)
	{
		public ProblemDetails ToProblemDetails()
		{
			var status = error.Type.ToHttpStatusCode();

			return new()
			{
				Status = status,
				Title = error.Type.ToTitle(),
				Detail = status == StatusCodes.Status500InternalServerError
					? "An unexpected error occurred" // Do not leak internal details to the client
					: error.Description,
				Extensions = { ["code"] = error.Code }
			};
		}
	}
}
