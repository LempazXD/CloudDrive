using AutoMapper;
using CloudDrive.Application.DTOs;
using CloudDrive.Application.Interfaces;
using CloudDrive.Domain.Interfaces;

namespace CloudDrive.Infrastructure.Services;

public class FileService : IFileService
{
	private readonly IFileRepository _fileRep;
	private readonly IMapper _mapper;
	private readonly IStorageService _storageService;

	public FileService(IFileRepository fileRep, IMapper mapper, IStorageService storageService)
	{
		_fileRep = fileRep;
		_mapper = mapper;
		_storageService = storageService;
	}

	public async Task<List<FileDto>> GetUserFiles(Guid userId)
	{
		var files = await _fileRep.GetAllByUserId(userId);
		return _mapper.Map<List<FileDto>>(files);
	}

	public async Task<FileDto?> GetFileById(Guid id)
	{
		var file = await _fileRep.GetOneById(id);
		return file == null ? null : _mapper.Map<FileDto>(file);
	}

	public async Task UploadFile(Guid userId, string fileName, Stream fileStream)
	{/*
		// !!! Сначала лучше записывать метаданные в БД и потом добавлять сам файл или наоборот?
		var file = new FileEntity();
		await _fileRep.Add(file);
		await _fileRep.SaveChanges(); // Записываем метаданные файла в БД
		await _storageService.SaveFile(); // Сохраняем файл в хранилище
	*/}

	public async Task MoveToTrash(Guid id)
	{
		await _fileRep.MoveToTrashById(id);
		await _fileRep.SaveChanges();

		// !!! Добавить реализацию перемещения в корзину
		// Создать отдельную папку для корзины пользователя или хранить всё в одной папке (и обычные файлы, и удалённые)
	}

	public async Task RestoreFromTrash(Guid id)
	{
		await _fileRep.RestoreById(id);
		await _fileRep.SaveChanges();

		// !!! Добавить реализацию восстановления
	}

	public async Task DeletePermanently(Guid id)
	{
		await _fileRep.DeletePermanentlyById(id);
		await _fileRep.SaveChanges();

		// !!! Добавить реализацию удаления из корзины (полное удаление)
	}
}
