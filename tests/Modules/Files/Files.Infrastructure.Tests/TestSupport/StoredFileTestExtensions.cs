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

	private static readonly PropertyInfo DeletedAtUtcProperty =
		typeof(StoredFile).GetProperty(nameof(StoredFile.DeletedAtUtc))!;

	public static StoredFile SetStatus(this StoredFile file, FileStatus status)
	{
		StatusProperty.SetValue(file, status);
		return file;
	}

	// DeletedAtUtc тоже переходит в непустое состояние только через ExecuteUpdateAsync
	// (StoredFileRepository.SoftDeleteAsync) - тот же приём, что SetStatus.
	public static StoredFile SetDeletedAtUtc(this StoredFile file, DateTimeOffset? deletedAtUtc)
	{
		DeletedAtUtcProperty.SetValue(file, deletedAtUtc);
		return file;
	}
}
