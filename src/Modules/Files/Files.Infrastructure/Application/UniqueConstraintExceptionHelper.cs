using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Files.Infrastructure.Application;

internal static class UniqueConstraintExceptionHelper
{
	/// <summary>
	/// true для нарушения partial-unique-индекса на имени - что при обычном
	/// SaveChangesAsync (create), что при ExecuteUpdateAsync (rename). ExecuteUpdateAsync
	/// эмпирически подтверждён (см. src/Modules/CLAUDE.md, реальный Postgres, .NET 10 / EF Core
	/// 10.0.9 / Npgsql) пробрасывающим сырой Npgsql.PostgresException - в отличие от
	/// SaveChangesAsync, который оборачивает его в DbUpdateException. Обе формы перехватываются
	/// здесь всё равно и намеренно навсегда, а не только подтверждённая: узкий catch, если
	/// будущий апгрейд EF Core/Npgsql незаметно сменит форму, тихо провалил бы конфликт в
	/// GlobalExceptionHandler как 500 вместо 409.
	/// </summary>
	internal static bool IsUniqueViolation(Exception ex) =>
		ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } ||
		ex is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}
