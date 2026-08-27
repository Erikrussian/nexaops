using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Users.DTOs;

namespace NexaOps.Application.Auth.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> GetMeAsync(CancellationToken cancellationToken = default);
}
