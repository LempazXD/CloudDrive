namespace Files.Core.Application.Abstractions;

/// <summary>
/// Исход попытки переместить папку на уровне репозитория. Не bool, потому что у folder move,
/// в отличие от rename/file move, есть третий, семантически отдельный исход - см.
/// IFolderRepository.MoveAsync и ADR 0016. Конфликт по имени сюда не входит - он, как и у
/// rename, остаётся исключением (UniqueConstraintExceptionHelper), пойманным в сервисе.
/// </summary>
public enum FolderMoveOutcome
{
	Moved,
	NotFound,
	WouldCreateCycle
}
