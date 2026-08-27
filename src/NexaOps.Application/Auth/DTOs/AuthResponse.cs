using NexaOps.Application.Users.DTOs;

namespace NexaOps.Application.Auth.DTOs;

public class AuthResponse
{
    public string Message { get; init; } = string.Empty;
    public UserResponse User { get; init; } = default!;
    public string AccessToken { get; init; } = string.Empty;
}
