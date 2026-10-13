using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Users.DTOs;

namespace NexaOps.Application.Auth.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? refreshToken, string? ipAddress = null, CancellationToken cancellationToken = default);
    Task LogoutAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSessionResponse>> GetActiveSessionsAsync(string? currentRefreshToken = null, CancellationToken cancellationToken = default);
    Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<UserResponse> GetMeAsync(CancellationToken cancellationToken = default);
}
