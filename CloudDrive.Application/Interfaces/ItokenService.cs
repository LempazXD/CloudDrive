using CloudDrive.Domain.Entities;

namespace CloudDrive.Application.Interfaces;

public interface ItokenService
{
	public string CreateToken(UserEntity user);
}
