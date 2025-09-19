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

var key = Encoding.UTF8.GetBytes("super_secret_key_12345"); // ������ ��� �������

// DbContext
builder.Services.AddDbContext<CloudDriveDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("CloudDriveDb")));

// �����������
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMailCodeRepository, MailCodeRepository>();

// �����������
builder.Services.AddAutoMapper(typeof(FileMappingProfile).Assembly, typeof(UserMappingProfile).Assembly);

// �������
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<ItokenService, TokenService	>();

/* builder.Services.AddAuthentication(options =>
{
	options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
	options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuer = false, // ���� �� ��������� ��������
		ValidateAudience = false, // � ����������
		ValidateLifetime = true, // ��������� ���� �����
		ValidateIssuerSigningKey = true, // ��������� �������
		IssuerSigningKey = new SymmetricSecurityKey(key) // ����
	};
}); */

// ������
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		policy.AllowAnyOrigin();
		policy.AllowAnyHeader();
		policy.AllowAnyMethod();
	});
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors();
app.MapControllers();

app.Run();
