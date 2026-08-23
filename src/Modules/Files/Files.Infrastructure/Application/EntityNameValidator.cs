using System.Globalization;
using Shared.Kernel.Results;

namespace Files.Infrastructure.Application;

/// <summary>
/// Общая валидация/нормализация имени файла и папки - используется и при создании, и при
/// переименовании, чтобы оба пути принимали одинаково допустимые имена (см. Files.Folder.NameTooLong
/// в src/Modules/CLAUDE.md).
/// </summary>
internal static class EntityNameValidator
{
	internal static Result<string> Validate(string? rawName, int maxLength, string invalidCode, string tooLongCode)
	{
		var name = rawName?.Trim() ?? string.Empty;

		if (name.Length == 0 || name is "." or "..")
			return Error.Validation(invalidCode);

		foreach (var c in name)
		{
			// Control (Cc) - блокирует \r/\n/\0 и т.п., которые ломают синтаксис заголовка
			// Content-Disposition при скачивании (SeaweedFsBlobStorage строит его сырой
			// интерполяцией, без экранирования). Format (Cf) - ловит невидимые и
			// bidi-override символы (например, U+202E), которые не относятся к Control, но
			// точно так же проходят мимо char.IsWhiteSpace/Trim(). '"' - отдельно, ломает тот
			// же заголовок, но сама по себе не Control/Format.
			if (char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format || c == '"')
				return Error.Validation(invalidCode);
		}

		return name.Length > maxLength ? Error.Validation(tooLongCode) : Result.Success(name);
	}
}
