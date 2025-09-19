using CloudDrive.Domain.Entities;
using CloudDrive.Domain.Enums;

namespace CloudDrive.Domain.Interfaces;

public interface IMailCodeRepository
{
	Task<MailCodeEntity?> FindByEmail(string usernameOrEmail);
	Task Add(MailCodeEntity authCode);
	void Update(MailCodeEntity authCode);
	Task SaveChanges();
}
