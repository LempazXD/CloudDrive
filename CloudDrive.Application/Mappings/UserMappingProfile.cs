using AutoMapper;
using CloudDrive.Application.DTOs;
using CloudDrive.Domain.Entities;

namespace CloudDrive.Application.Mappings;

public class UserMappingProfile : Profile
{
	public UserMappingProfile()
	{
		CreateMap<UserEntity, UserDto>();
	}
}
