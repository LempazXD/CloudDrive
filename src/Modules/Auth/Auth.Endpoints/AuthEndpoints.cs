using Auth.Core.Application.Abstractions;
using Auth.Endpoints.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Shared.Api.Extensions;

namespace Auth.Endpoints;

public static class AuthEndpoints
{
	public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/auth").WithTags("Auth");

		group.MapPost("/register", RegisterAsync);
		group.MapPost("/login", LoginAsync);
		group.MapPost("/refresh", RefreshAsync);
		group.MapPost("/logout", LogoutAsync);

		return app;
	}

	private static async Task<Results<Ok<RegisterResponse>, ProblemHttpResult>> RegisterAsync(
		RegisterRequest request, IAuthService authService, CancellationToken ct)
	{
		var result = await authService.RegisterAsync(request.Username, request.Email, request.Password, ct);
		return result.Match(s => TypedResults.Ok(new RegisterResponse(s.UserId, s.Username, s.Email)));
	}

	private static async Task<Results<Ok<AuthTokensResponse>, ProblemHttpResult>> LoginAsync(
		LoginRequest request, IAuthService authService, CancellationToken ct)
	{
		var result = await authService.LoginAsync(request.Login, request.Password, ct);
		return result.Match(ToResponse);
	}

	private static async Task<Results<Ok<AuthTokensResponse>, ProblemHttpResult>> RefreshAsync(
		RefreshRequest request, IAuthService authService, CancellationToken ct)
	{
		var result = await authService.RefreshAsync(request.RefreshToken, ct);
		return result.Match(ToResponse);
	}

	private static async Task<Results<Ok, ProblemHttpResult>> LogoutAsync(
		LogoutRequest request, IAuthService authService, CancellationToken ct)
	{
		var result = await authService.LogoutAsync(request.RefreshToken, ct);
		return result.Match(TypedResults.Ok);
	}

	private static Ok<AuthTokensResponse> ToResponse(AuthTokens tokens) => TypedResults.Ok(
		new AuthTokensResponse(
			tokens.AccessToken,
			tokens.AccessTokenExpiresAtUtc,
			tokens.RefreshToken,
			tokens.RefreshTokenExpiresAtUtc));
}
