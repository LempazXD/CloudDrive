﻿namespace CloudDrive.Application.DTOs.Requests;

public class RegisterRequestDto
{
	public string Username { get; set; } = default!;
	public string Email { get; set; } = default!;
	public string Password { get; set; } = default!;
}
