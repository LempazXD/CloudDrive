using CloudDrive.Domain.Entities;

namespace CloudDrive.Domain.Interfaces;

public interface IUserRepository
{
	Task<UserEntity?> FindByUsername(string username);
	Task<UserEntity?> FindByEmail(string email);
	Task<bool> UserExistsByUsernameOrEmail(string username, string email);
	Task SaveChanges();
	Task Add(UserEntity user);
}
