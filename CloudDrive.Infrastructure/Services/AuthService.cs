using CloudDrive.Application.Interfaces;
using CloudDrive.Application.Requests;
using CloudDrive.Domain.Enums;
using CloudDrive.Domain.Interfaces;

namespace CloudDrive.Infrastructure.Services;

public class AuthService : IAuthService
{
	private readonly IUserRepository _userRep;
	private readonly IEmailService _emailService;
	private readonly IMailCodeRepository _mailCodeRep;
	private readonly ItokenService _tokenService;

	private const int _mailCodeExpirationMinutes = 5;

	public AuthService(IUserRepository userRep, IEmailService emailService, IMailCodeRepository authCodeRep, ItokenService tokenService)
	{
		_userRep = userRep;
		_emailService = emailService;
		_mailCodeRep = authCodeRep;
		_tokenService = tokenService;
	}

	public async Task SendRegisterCode(SendRegisterCodeRequest request)
	{
		if (await _userRep.UserExistsByUsernameOrEmail(request.Username, request.Email))
			throw new Exception("Пользователь с таким логином или email уже существует");

		_emailService.PreSendMailCode(request.Email, MailCodeType.Registration);

		// Тут добавить запись в таблицу со временными данными для регистрации
	}

	public async Task<string> Register(RegisterRequest request)
	{
		return "";
	}

	public async Task<string> Login(LoginRequest request)
	{
		var user = await _userRep.FindByEmail(request.UsernameOrEmail)
			?? await _userRep.FindByUsername(request.UsernameOrEmail);

		if (user == null || !BCrypt.Net.BCrypt.EnhancedVerify(request.Password, user.Password))
			throw new Exception("Неверный логин или пароль");

		return _tokenService.CreateToken(user!);
	}

	public async Task<string> LoginAuthCode(MailCodeLoginRequest request)
	{
		var user = await _userRep.FindByEmail(request.UsernameOrEmail)
			?? await _userRep.FindByUsername(request.UsernameOrEmail);

		_emailService.PreSendMailCode(user?.Email, MailCodeType.Login);

		return "а";
	}

	public async Task<string?> VerifyMailCode(string usernameOrEmail, string code)
	{
		var user = await _userRep.FindByEmail(usernameOrEmail)
			?? await _userRep.FindByUsername(usernameOrEmail);

		var authCode = await _mailCodeRep.FindByEmail(user?.Email);

		if (authCode == null) // Избавиться от этого
			throw new Exception("Код не найден");

		bool isExpired = DateTime.UtcNow > authCode.CreatedAt.ToUniversalTime().AddMinutes(_mailCodeExpirationMinutes);
		if (authCode.FailedAttempts >= 3 || isExpired)
			throw new Exception("Код недействителен");

		if (code != authCode.Code)
		{
			authCode.WrongCode();
			_mailCodeRep.Update(authCode);
			await _mailCodeRep.SaveChanges();
			throw new Exception("Неверный код");
		}

		authCode.ResetCode();
		_mailCodeRep.Update(authCode);
		await _mailCodeRep.SaveChanges();

		return _tokenService.CreateToken(user);
		// !!! Если пользоваель null, всё будет норм?
		// !!! Если возвращать null, всё будет норм или нужны проверки?
	}
}
