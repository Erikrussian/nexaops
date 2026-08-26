using System.ComponentModel.DataAnnotations;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Users.DTOs;

public class UpdateUserRequest
{
    public string? Name { get; set; }

    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string? Email { get; set; }

    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string? Password { get; set; }

    public UserRole? Role { get; set; }
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }
}
