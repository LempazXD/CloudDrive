namespace CloudDrive.Domain.Enums;

public enum FileStatusType
{
	Pending, // Создана запись, но файл ещё не загружен
	Uploading, // Идёт загрузка
	Completed, // Загружен и доступен
	Failed, // Ошибка при загрузке
	Deleted, // В корзине
	HardDeleted // Удалён окончательно
}
