using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models.DTOs;

public class LoginRequestDto
{
    [Required(ErrorMessage = "يرجى إدخال البريد الإلكتروني أو رقم الهاتف أو اسم المستخدم.")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "يرجى إدخال كلمة المرور.")]
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "يرجى إدخال البريد الإلكتروني أو رقم الهاتف لاستعادة كلمة المرور.")]
    public string RecoveryIdentifier { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class DetectIdentifierRequestDto
{
    [Required]
    public string Identifier { get; set; } = string.Empty;
}

public class DetectIdentifierResponseDto
{
    public string Type { get; set; } = string.Empty;
}
