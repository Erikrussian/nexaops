using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Users.DTOs;
using NexaOps.Application.Users.Services;
using NexaOps.Domain.Entities;
using NexaOps.Application.Common.Security;

namespace NexaOps.Application.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private const int RefreshTokenExpirationDays = 7;

    public AuthService(
        IUserService userService,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        ILogger<AuthService> logger)
    {
        _userService = userService;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUserService = currentUserService;
        _context = context;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var createUserRequest = new CreateUserRequest
        {
            Name = request.Name,
            Email = request.Email,
            Password = request.Password
        };

        var userResponse = await _userService.CreateAsync(createUserRequest, cancellationToken);
        var user = await _userService.GetByEmailWithPasswordAsync(request.Email, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Không thể tìm thấy tài khoản vừa tạo");
        }

        var accessToken = _jwtTokenGenerator.GenerateToken(user);
        var (refreshToken, session) = CreateRefreshTokenSession(user.Id, ipAddress, userAgent);

        _context.RefreshTokens.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Người dùng {UserId} đăng ký tài khoản thành công", user.Id);

        return new AuthResponse
        {
            Message = "Đăng ký tài khoản thành công",
            User = userResponse,
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userService.GetByEmailWithPasswordAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Đăng nhập thất bại: email {Email} không tồn tại", request.Email);
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác");
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Đăng nhập thất bại: mật khẩu không hợp lệ cho {Email}", request.Email);
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Đăng nhập thất bại: tài khoản {Email} đã bị vô hiệu hóa", request.Email);
            throw new UnauthorizedAccessException("Tài khoản của bạn đã bị vô hiệu hóa");
        }

        var accessToken = _jwtTokenGenerator.GenerateToken(user);
        var (refreshToken, session) = CreateRefreshTokenSession(user.Id, ipAddress, userAgent);

        _context.RefreshTokens.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Người dùng {UserId} ({Email}) đăng nhập thành công", user.Id, user.Email);

        return new AuthResponse
        {
            Message = "Đăng nhập thành công",
            User = UserResponse.FromEntity(user),
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token không được để trống");
        }

        var tokenHash = CryptoUtils.HashToken(refreshToken);

        var tokenRecord = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (tokenRecord is null)
        {
            _logger.LogWarning("Yêu cầu refresh token không tồn tại từ IP {IpAddress}", ipAddress);
            throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã bị thu hồi.");
        }

        // 1. REUSE DETECTION: Phát hiện token đã bị thu hồi trước đó được gửi lại
        if (tokenRecord.IsRevoked)
        {
            _logger.LogCritical(
                "CẢNH BÁO AN NINH: Phát hiện tái sử dụng refresh token đã thu hồi ({TokenId}) của người dùng {UserId} từ IP {IpAddress}! Thu hồi toàn bộ phiên của token family.",
                tokenRecord.Id,
                tokenRecord.UserId,
                ipAddress);

            // Thu hồi toàn bộ active refresh tokens của user này
            var familyTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == tokenRecord.UserId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var t in familyTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedByIp = ipAddress;
                t.ReasonRevoked = "Token family breach detected (reused revoked token)";
            }

            await _context.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedAccessException("Phát hiện bất thường về phiên đăng nhập. Toàn bộ phiên đã bị thu hồi để bảo vệ an toàn tài khoản.");
        }

        // 2. Kiểm tra hết hạn
        if (tokenRecord.IsExpired)
        {
            _logger.LogInformation("Refresh token {TokenId} đã hết hạn", tokenRecord.Id);
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        // 3. Kiểm tra người dùng
        if (tokenRecord.User is null || !tokenRecord.User.IsActive)
        {
            throw new UnauthorizedAccessException("Tài khoản người dùng không hợp lệ hoặc đã bị vô hiệu hóa.");
        }

        // 4. ROTATION: Thu hồi token hiện tại và sinh token mới
        var (newRefreshToken, newSession) = CreateRefreshTokenSession(tokenRecord.UserId, ipAddress, userAgent);

        tokenRecord.RevokedAt = DateTime.UtcNow;
        tokenRecord.RevokedByIp = ipAddress;
        tokenRecord.ReplacedByTokenHash = newSession.TokenHash;
        tokenRecord.ReasonRevoked = "Rotated";

        _context.RefreshTokens.Add(newSession);

        var newAccessToken = _jwtTokenGenerator.GenerateToken(tokenRecord.User);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotation thành công cho người dùng {UserId}", tokenRecord.UserId);

        return new AuthResponse
        {
            Message = "Làm mới phiên đăng nhập thành công",
            User = UserResponse.FromEntity(tokenRecord.User),
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };
    }

    public async Task LogoutAsync(
        string? refreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var tokenHash = CryptoUtils.HashToken(refreshToken);
            var tokenRecord = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

            if (tokenRecord is not null && tokenRecord.IsActive)
            {
                tokenRecord.RevokedAt = DateTime.UtcNow;
                tokenRecord.RevokedByIp = ipAddress;
                tokenRecord.ReasonRevoked = "User logout";
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Đã thu hồi phiên {TokenId} khi người dùng đăng xuất", tokenRecord.Id);
                return;
            }
        }

        // Nếu không có refresh token nhưng có Authorization header
        if (_currentUserService.UserId.HasValue)
        {
            _logger.LogInformation("Người dùng {UserId} thực hiện đăng xuất", _currentUserService.UserId.Value);
        }
    }

    public async Task LogoutAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        var userId = _currentUserService.UserId.Value;

        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var t in activeTokens)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.ReasonRevoked = "User logout-all requested";
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Đã thu hồi tất cả {Count} phiên làm việc của người dùng {UserId}", activeTokens.Count, userId);
    }

    public async Task<IReadOnlyList<UserSessionResponse>> GetActiveSessionsAsync(
        string? currentRefreshToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        var userId = _currentUserService.UserId.Value;
        var currentHash = !string.IsNullOrWhiteSpace(currentRefreshToken)
            ? CryptoUtils.HashToken(currentRefreshToken)
            : null;

        var activeTokens = await _context.RefreshTokens
            .AsNoTracking()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync(cancellationToken);

        return activeTokens.Select(rt => new UserSessionResponse
        {
            Id = rt.Id,
            CreatedAt = rt.CreatedAt,
            ExpiresAt = rt.ExpiresAt,
            CreatedByIp = rt.CreatedByIp,
            UserAgent = rt.UserAgent,
            IsCurrent = currentHash != null && rt.TokenHash == currentHash
        }).ToList();
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        var userId = _currentUserService.UserId.Value;

        var session = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Id == sessionId, cancellationToken);

        if (session is null)
        {
            throw new KeyNotFoundException("Không tìm thấy phiên làm việc này");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền thu hồi phiên của người dùng khác");
        }

        if (session.IsActive)
        {
            session.RevokedAt = DateTime.UtcNow;
            session.ReasonRevoked = "Revoked by user";
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Người dùng {UserId} đã thu hồi phiên {SessionId}", userId, sessionId);
        }
    }

    public async Task<UserResponse> GetMeAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        return await _userService.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
    }

    private (string PlaintextToken, RefreshToken Session) CreateRefreshTokenSession(
        Guid userId,
        string? ipAddress,
        string? userAgent)
    {
        var plaintextToken = CryptoUtils.GenerateSecureToken();
        var tokenHash = CryptoUtils.HashToken(plaintextToken);

        var expirationDays = RefreshTokenExpirationDays;

        var session = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            UserAgent = userAgent
        };

        return (plaintextToken, session);
    }
}
