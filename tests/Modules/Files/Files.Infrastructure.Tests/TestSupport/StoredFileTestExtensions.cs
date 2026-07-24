using System.Reflection;
using Files.Core.Domain;

namespace Files.Infrastructure.Tests.TestSupport;

// Status переходит в Completed/Failed только через ExecuteUpdateAsync в StoredFileRepository
// (см. MarkCompletedAsync/MarkFailedAsync) либо через материализацию EF Core уже обновлённой
// строки - никогда через доменный метод. Reflection здесь повторяет тот же путь (прямая запись в
// private-setter), а не обходит инкапсуляцию - тот же приём, что RefreshTokenTestExtensions
// использует для RevokedAtUtc в Auth.Infrastructure.Tests.
internal static class StoredFileTestExtensions
{
	private static readonly PropertyInfo StatusProperty =
		typeof(StoredFile).GetProperty(nameof(StoredFile.Status))!;

	public static StoredFile SetStatus(this StoredFile file, FileStatus status)
	{
		StatusProperty.SetValue(file, status);
		return file;
	}
}
