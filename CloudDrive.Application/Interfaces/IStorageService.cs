namespace CloudDrive.Application.Interfaces;

public interface IStorageService
{
	public Task CreateUserFolder(string username);
	public Task SaveFile();
}
