using CloudDrive.Domain.Entities;

namespace CloudDrive.Domain.Interfaces;

public interface IAuthCodeRepository
{
	Task<AuthCodeEntity?> FindByUsernameOrEmail(string usernameOrEmail);
	Task Add(AuthCodeEntity authCode);
	void Update(AuthCodeEntity authCode);
	Task SaveChanges();
}
