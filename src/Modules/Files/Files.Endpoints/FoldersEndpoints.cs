using System.Security.Claims;
using Files.Core.Application.Abstractions;
using Files.Endpoints.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Shared.Api.Extensions;

namespace Files.Endpoints;

public static class FoldersEndpoints
{
	public static IEndpointRouteBuilder MapFoldersEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/folders").WithTags("Folders").RequireAuthorization();

		group.MapPost("/", CreateFolderAsync);
		group.MapPost("/{id:guid}/rename", RenameFolderAsync);
		group.MapGet("/{id:guid}", GetFolderAsync);
		group.MapDelete("/{id:guid}", DeleteFolderAsync);

		return app;
	}

	private static async Task<Results<Ok<FolderResponse>, ProblemHttpResult>> CreateFolderAsync(
		CreateFolderRequest request, ClaimsPrincipal user, IFolderService folderService, CancellationToken ct)
	{
		var result = await folderService.CreateFolderAsync(user.GetOwnerId(), request.ParentFolderId, request.Name, ct);
		return result.Match(f => TypedResults.Ok(ToFolderResponse(f)));
	}

	private static async Task<Results<Ok<FolderResponse>, ProblemHttpResult>> RenameFolderAsync(
		Guid id, RenameFolderRequest request, ClaimsPrincipal user, IFolderService folderService, CancellationToken ct)
	{
		var result = await folderService.RenameFolderAsync(user.GetOwnerId(), id, request.Name, ct);
		return result.Match(f => TypedResults.Ok(ToFolderResponse(f)));
	}

	private static async Task<Results<Ok<FolderResponse>, ProblemHttpResult>> GetFolderAsync(
		Guid id, ClaimsPrincipal user, IFolderService folderService, CancellationToken ct)
	{
		var result = await folderService.GetFolderAsync(user.GetOwnerId(), id, ct);
		return result.Match(f => TypedResults.Ok(ToFolderResponse(f)));
	}

	private static async Task<Results<Ok, ProblemHttpResult>> DeleteFolderAsync(
		Guid id, ClaimsPrincipal user, IFolderService folderService, CancellationToken ct)
	{
		var result = await folderService.DeleteFolderAsync(user.GetOwnerId(), id, ct);
		return result.Match(TypedResults.Ok);
	}

	private static FolderResponse ToFolderResponse(FolderSummary folder) =>
		new(folder.Id, folder.ParentFolderId, folder.Name, folder.CreatedAtUtc);
}
