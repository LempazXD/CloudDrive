using System.Security.Claims;
using Files.Core.Application.Abstractions;
using Files.Endpoints.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Shared.Api.Extensions;

namespace Files.Endpoints;

public static class FilesEndpoints
{
	public static IEndpointRouteBuilder MapFilesEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/files").WithTags("Files").RequireAuthorization();

		group.MapPost("/", InitiateUploadAsync);
		group.MapPost("/{id:guid}/complete", CompleteUploadAsync);
		group.MapGet("/", ListFilesAsync);
		group.MapGet("/{id:guid}/download", GetDownloadUrlAsync);
		group.MapDelete("/{id:guid}", DeleteFileAsync);

		return app;
	}

	private static async Task<Results<Ok<InitiateUploadResponse>, ProblemHttpResult>> InitiateUploadAsync(
		InitiateUploadRequest request, ClaimsPrincipal user, IFilesService filesService, CancellationToken ct)
	{
		var result = await filesService.InitiateUploadAsync(
			user.GetOwnerId(),
			request.FolderId,
			request.OriginalFileName,
			request.ContentType,
			request.SizeBytes,
			request.Sha256Declared,
			ct);

		return result.Match(s => TypedResults.Ok(new InitiateUploadResponse(
			s.FileId,
			s.UploadId,
			s.Parts.Select(p => new UploadPartResponse(p.PartNumber, p.PresignedUrl)).ToList())));
	}

	private static async Task<Results<Ok<FileResponse>, ProblemHttpResult>> CompleteUploadAsync(
		Guid id, CompleteUploadRequest request, ClaimsPrincipal user, IFilesService filesService, CancellationToken ct)
	{
		var parts = (request.Parts ?? [])
			.Select(p => new BlobUploadedPart(p.PartNumber, p.ETag))
			.ToList();

		var result = await filesService.CompleteUploadAsync(user.GetOwnerId(), id, parts, ct);

		return result.Match(file => TypedResults.Ok(ToFileResponse(file)));
	}

	private static async Task<Results<Ok<FileListResponse>, ProblemHttpResult>> ListFilesAsync(
		[AsParameters] ListFilesRequest request, ClaimsPrincipal user, IFilesService filesService, CancellationToken ct)
	{
		var result = await filesService.ListFilesAsync(
			user.GetOwnerId(), request.FolderId, request.Cursor, request.Limit ?? 20, ct);

		return result.Match(page => TypedResults.Ok(
			new FileListResponse(page.Items.Select(ToFileResponse).ToList(), page.NextCursor)));
	}

	private static async Task<Results<Ok<DownloadUrlResponse>, ProblemHttpResult>> GetDownloadUrlAsync(
		Guid id, ClaimsPrincipal user, IFilesService filesService, CancellationToken ct)
	{
		var result = await filesService.GetDownloadUrlAsync(user.GetOwnerId(), id, ct);
		return result.Match(url => TypedResults.Ok(new DownloadUrlResponse(url)));
	}

	private static async Task<Results<Ok, ProblemHttpResult>> DeleteFileAsync(
		Guid id, ClaimsPrincipal user, IFilesService filesService, CancellationToken ct)
	{
		var result = await filesService.DeleteFileAsync(user.GetOwnerId(), id, ct);
		return result.Match(TypedResults.Ok);
	}

	private static FileResponse ToFileResponse(FileSummary file) => new(
		file.Id,
		file.FolderId,
		file.OriginalFileName,
		file.ContentType,
		file.SizeBytes,
		file.Status.ToString(),
		file.CreatedAtUtc);
}
