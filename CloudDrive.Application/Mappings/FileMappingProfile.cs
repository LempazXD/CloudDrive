using AutoMapper;
using CloudDrive.Application.DTOs;
using CloudDrive.Domain.Entities;

namespace CloudDrive.Application.Mappings;

public class FileMappingProfile : Profile
{
	public FileMappingProfile()
	{
		CreateMap<FileEntity, FileDto>()
			.ForMember(dest => dest.IconUrl,
					   opt => opt.MapFrom(src => GetIconByExtension(src.Extension)));
	}

	private string GetIconByExtension(string extension)
	{
		return extension.ToLower() switch
		{
			".pdf" => "/icons/pdf.png",
			".jpg" or ".jpeg" or ".png" => "/icons/image.png",
			".zip" => "/icons/archive.png",
			".txt" => "/icons/text.png",
			_ => "/icons/file.png",
		};
	}
}
