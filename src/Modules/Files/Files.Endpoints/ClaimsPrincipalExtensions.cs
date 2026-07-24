using System.Security.Claims;

namespace Files.Endpoints;

internal static class ClaimsPrincipalExtensions
{
	// JwtBearerOptions.MapInboundClaims = false оставляет типы клеймов ровно такими,
	// как они закодированы в токене - "sub" читается по короткому
	// имени напрямую, без переотображения в длинный URI claim-типа .NET.
	public static Guid GetOwnerId(this ClaimsPrincipal user) =>
		Guid.Parse(user.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing 'sub' claim."));
}
