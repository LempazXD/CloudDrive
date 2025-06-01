using CloudDrive.Application.DTOs.Requests;
using CloudDrive.Application.Interfaces;
using CloudDrive.Domain.Entities;
using CloudDrive.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CloudDrive.Infrastructure.Services;

public class AuthService : IAuthService
{
	private readonly IConfiguration _config;
	private readonly IUserRepository _userRep;
	private readonly IEmailService _emailService;
	private readonly IAuthCodeRepository _authCodeRep;

	private const int _authCodeExpirationMinutes = 5;
	private const string _authCodeSymbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
	private const int _authCodeLength = 6;

	public AuthService(IUserRepository userRep, IConfiguration config, IEmailService emailService, IAuthCodeRepository authCodeRep)
	{
		_config = config;
		_userRep = userRep;
		_emailService = emailService;
		_authCodeRep = authCodeRep;
	}

	public async Task<string> Login(LoginRequestDto request)
	{
		var user = await _userRep.FindByEmail(request.UsernameOrEmail)
			?? await _userRep.FindByUsername(request.UsernameOrEmail);

		if (user == null || !BCrypt.Net.BCrypt.EnhancedVerify(request.Password, user.Password))
			throw new Exception("Неверный логин или пароль");

		return GenerateJwtToken(user!);
	}

	public async Task LoginAuthCode(LoginAuthCodeRequestDto request)
	{
		var user = await _userRep.FindByEmail(request.LoginOrEmail)
			?? await _userRep.FindByUsername(request.LoginOrEmail);

		var newCode = GenerateAuthCode();

		var authCode = await _authCodeRep.FindByUsernameOrEmail(request.LoginOrEmail);
		var now = DateTime.UtcNow;

		if (authCode == null)
		{
			authCode = new AuthCodeEntity
			{
				UsernameOrEmail = request.LoginOrEmail,
				Code = newCode,
				CreatedAt = now,
				FailedAttempts = 0,
				SentCodeCount = 1
			};

			await _authCodeRep.Add(authCode);
		}
		else
		{
			var delay = GetAuthCodeDelay(authCode.SentCodeCount);
			var nextAvailableTime = authCode.CreatedAt.ToUniversalTime().AddMinutes(delay);

			if (now < nextAvailableTime)
			{
				//var secondsLeft = Math.Ceiling((nextAvailableTime - now).TotalSeconds);
				var secondsLeft = (nextAvailableTime - now).TotalSeconds;
				throw new Exception($"Подождите {secondsLeft} секунд перед отправкой нового кода");
			}

			authCode.Code = newCode;
			authCode.CreatedAt = DateTime.UtcNow;
			authCode.FailedAttempts = 0;
			authCode.SentCodeCount++;

			_authCodeRep.Update(authCode);
		}

		await _authCodeRep.SaveChanges();
		if (user != null)
			await _emailService.SendAuthCode(user.Email, newCode);

	}

	public async Task<string?> VerifyAuthCode(VerifyAuthCodeRequestDto request)
	{
		var authCode = await _authCodeRep.FindByUsernameOrEmail(request.LoginOrEmail);

		if (authCode == null)
			throw new Exception("Код не найден");

		bool isExpired = DateTime.UtcNow > authCode.CreatedAt.ToUniversalTime().AddMinutes(_authCodeExpirationMinutes);
		if (authCode.FailedAttempts >= 3 || isExpired)
			throw new Exception("Код недействителен");

		if (request.Code != authCode.Code)
		{
			authCode.FailedAttempts++;
			_authCodeRep.Update(authCode);
			await _authCodeRep.SaveChanges();
			throw new Exception("Неверный код");
		}

		var user = await _userRep.FindByEmail(request.LoginOrEmail)
			?? await _userRep.FindByUsername(request.LoginOrEmail);

		if (user == null)
			throw new Exception("Пользователь не найден");

		authCode.Code = null;
		_authCodeRep.Update(authCode);
		await _authCodeRep.SaveChanges();

		return GenerateJwtToken(user);
	}

	public async Task Register(RegisterRequestDto request)
	{
		if (await _userRep.UserExistsByUsernameOrEmail(request.Username, request.Email))
			throw new Exception("Пользователь с таким логином или email уже существует");

		var user = new UserEntity
		{
			Username = request.Username,
			Email = request.Email,
			Password = BCrypt.Net.BCrypt.EnhancedHashPassword(request.Password),
			CreatedAt = DateTime.UtcNow,
		};

		await _userRep.Add(user);
		await _userRep.SaveChanges();

		await Task.Run(() => Directory.CreateDirectory($"C:\\storage\\{user.Username}"));
	}

	private string GenerateJwtToken(UserEntity user)
	{
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var claims = new[]
		{
			new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
			new Claim(ClaimTypes.Name, user.Username),
			new Claim(ClaimTypes.Email, user.Email)
		};

		var token = new JwtSecurityToken(
			issuer: _config["Jwt:Issuer"],
			audience: _config["Jwt:Audience"],
			claims: claims,
			expires: DateTime.UtcNow.AddDays(7),
			signingCredentials: creds);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	private string GenerateAuthCode()
	{
		var code = new char[_authCodeLength];

		using var rng = RandomNumberGenerator.Create();
		var randomBytes = new byte[_authCodeLength];

		rng.GetBytes(randomBytes);

		for (int i = 0; i < _authCodeLength; i++)
		{
			int index = randomBytes[i] % _authCodeSymbols.Length;
			code[i] = _authCodeSymbols[index];
		}

		return new string(code);
	}

	private int GetAuthCodeDelay(int sentCodeCount)
	{
		if (sentCodeCount <= 1)
			return 1;
		else
			return Math.Min((int)Math.Pow(sentCodeCount, 2), 30);
	}
}
