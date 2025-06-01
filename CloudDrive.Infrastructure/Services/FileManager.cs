using AutoMapper;
using CloudDrive.Application.DTOs;
using CloudDrive.Application.Interfaces;
using CloudDrive.Domain.Entities;
using CloudDrive.Domain.Interfaces;

namespace CloudDrive.Infrastructure.Services;

public class FileManager : IFileManager
{
	private readonly IFileRepository _fileRep;
	private readonly IMapper _mapper;

	public FileManager(IFileRepository fileRep, IMapper mapper)
	{
		_fileRep = fileRep;
		_mapper = mapper;
	}

	public async Task<List<FileDto>> GetFiles(int userId)
	{
		var files = await _fileRep.GetAllByUserId(userId);
		return _mapper.Map<List<FileDto>>(files);
	}

	public async Task<FileDto?> GetFileById(int id)
	{
		var file = await _fileRep.GetOneById(id);
		return file == null ? null : _mapper.Map<FileDto>(file);
	}

	public async Task UploadFile(FileDto fileDto)
	{
		var file = _mapper.Map<FileEntity>(fileDto);
		await _fileRep.Add(file);
		await _fileRep.SaveChanges();

		// Добавить реализацию добавления файла
	}

	public async Task MoveToTrash(int id)
	{
		await _fileRep.MoveToTrashById(id);
		await _fileRep.SaveChanges();

		// Добавить реализацию перемещения в корзину
	}

	public async Task RestoreFromTrash(int id)
	{
		await _fileRep.RestoreById(id);
		await _fileRep.SaveChanges();

		// Добавить реализацию восстановления
	}

	public async Task DeletePermanently(int id)
	{
		await _fileRep.DeletePermanentlyById(id);
		await _fileRep.SaveChanges();

		// Добавить реализацию удаления из корзины
	}
}
