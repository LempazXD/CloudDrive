namespace Files.Core.Application.Pagination;

/// <summary>
/// Opaque-курсор для keyset-пагинации поверх GUID v7, поэтому отдельный ключ сортировки не нужен.
/// </summary>
public static class Cursor
{
	public static string Encode(Guid id) => Convert.ToBase64String(id.ToByteArray());

	public static bool TryDecode(string? cursor, out Guid id)
	{
		id = default;

		if (string.IsNullOrEmpty(cursor))
			return false;

		try
		{
			var bytes = Convert.FromBase64String(cursor);
			if (bytes.Length != 16)
				return false;

			id = new Guid(bytes);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
