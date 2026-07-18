namespace Auth.Core.Application.Abstractions;

/// <summary>
/// Кэширует ответ последней успешной ротации на короткое льготное окно, чтобы повторное
/// предъявление уже отозванного (только что заменённого) refresh-токена — например, из-за
/// потери ответа сети и повтора клиентом — переигрывало тот же результат вместо того, чтобы
/// трактоваться как кража и отзывать все токены пользователя.
/// </summary>
public interface IRefreshTokenReplayCache
{
	void Set(string consumedTokenHash, AuthTokens tokens);

	bool TryGet(string consumedTokenHash, out AuthTokens tokens);
}
