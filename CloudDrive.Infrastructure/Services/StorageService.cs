using CloudDrive.Application.Interfaces;

namespace CloudDrive.Infrastructure.Services;

public class StorageService : IStorageService
{
	private const string storagePath = $"C:\\storage";

	public async Task CreateUserFolder(string username)
	{
		await Task.Run(() => Directory.CreateDirectory(Path.Combine(storagePath, username)));
	}

	public async Task SaveFile()
	{
		// Тут файл сохраняет в хранилище
	}
}
