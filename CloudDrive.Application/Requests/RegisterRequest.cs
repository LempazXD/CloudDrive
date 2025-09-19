using System.ComponentModel.DataAnnotations;

namespace CloudDrive.Application.Requests;

public class RegisterRequest
{
	[Required]
	public string Token { get; set; } = default!;
	[Required]
	[Length(6, 6, ErrorMessage = "Код должен содержать 6 символов")]
	public string Code { get; set; } = default!;

}
