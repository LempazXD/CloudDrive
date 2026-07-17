using System.Diagnostics.CodeAnalysis;

namespace Shared.Api.Logging;

public sealed class SeqOptions
{
	public required string ServerUrl { get; init; }

	public static bool TryGetServerUri(string? serverUrl, [NotNullWhen(true)] out Uri? serverUri) =>
		Uri.TryCreate(serverUrl, UriKind.Absolute, out serverUri);
}
