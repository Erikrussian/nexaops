using System.ComponentModel.DataAnnotations;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Users.DTOs;

public class CreateUserRequest
{
    [Required(ErrorMessage = "Tên không được để trống")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    public UserRole? Role { get; set; } = UserRole.User;
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
}
