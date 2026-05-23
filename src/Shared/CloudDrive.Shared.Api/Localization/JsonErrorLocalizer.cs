using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudDrive.Shared.Api.Localization;

public sealed class JsonErrorLocalizer : IErrorLocalizer
{
	private const string FallbackCulture = "en";

	private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _translations;
	private readonly ILogger<JsonErrorLocalizer> _logger;

	public JsonErrorLocalizer(IHostEnvironment env, ILogger<JsonErrorLocalizer> logger)
	{
		_logger = logger;
		var path = Path.Combine(env.ContentRootPath, "Resources", "Localization");
		_translations = LoadAll(path);
	}

	public string Localize(string errorCode, string culture)
	{
		if (_translations.TryGetValue(culture, out var dict) && dict.TryGetValue(errorCode, out var msg))
			return msg;

		if (!string.Equals(culture, FallbackCulture, StringComparison.OrdinalIgnoreCase)
		    && _translations.TryGetValue(FallbackCulture, out var en)
		    && en.TryGetValue(errorCode, out var fallback))
		{
			_logger.LogWarning(
				"Localization key '{ErrorCode}' missing for culture '{Culture}', used '{Fallback}'.",
				errorCode, culture, FallbackCulture);
			return fallback;
		}

		_logger.LogWarning(
			"Localization key '{ErrorCode}' not found for culture '{Culture}' nor fallback '{Fallback}'.",
			errorCode, culture, FallbackCulture);
		return errorCode;
	}

	private static FrozenDictionary<string, FrozenDictionary<string, string>> LoadAll(string directory)
	{
		if (!Directory.Exists(directory))
			return FrozenDictionary<string, FrozenDictionary<string, string>>.Empty;

		var result = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

		foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
		{
			var culture = Path.GetFileNameWithoutExtension(file);

			using var stream = File.OpenRead(file);
			var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
				?? throw new InvalidOperationException(
					$"Localization file '{file}' contains 'null' instead of a JSON object.");

			result[culture] = entries.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
		}

		return result.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
	}
}