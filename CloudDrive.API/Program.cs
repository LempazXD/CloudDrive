using CloudDrive.Application.Interfaces;
using CloudDrive.Application.Mappings;
using CloudDrive.Domain.Interfaces;
using CloudDrive.Infrastructure.Persistence;
using CloudDrive.Infrastructure.Persistence.Repositories;
using CloudDrive.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Настройка DI

// DbContext
builder.Services.AddDbContext<CloudDriveDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("CloudDriveDb")));

// Репозитории
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthCodeRepository, AuthCodeRepository>();

// Автомапперы
builder.Services.AddAutoMapper(typeof(FileMappingProfile).Assembly, typeof(UserMappingProfile).Assembly);

// Сервисы
builder.Services.AddScoped<IFileManager, FileManager>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Прочее
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Jwt
builder.Services.AddAuthentication("Bearer")
	.AddJwtBearer("Bearer", options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = builder.Configuration["Jwt:Issuer"],
			ValidateAudience = true,
			ValidAudience = builder.Configuration["Jwt.Audience"],
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(
				Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
		};
	});

builder.Services.AddAuthorization();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
