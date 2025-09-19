using AutoMapper;
using CloudDrive.Application.DTOs;
using CloudDrive.Domain.Entities;

namespace CloudDrive.Application.Mappings;

public class AuthCodeMappingProfile : Profile
{
	public AuthCodeMappingProfile()
	{
		CreateMap<MailCodeEntity, MailCodeDto>();
	}
}
