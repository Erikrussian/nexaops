using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Users.DTOs;
using NexaOps.Application.Users.Services;

namespace NexaOps.Application.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUserService _currentUserService;

    public AuthService(
        IUserService userService,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUserService currentUserService)
    {
        _userService = userService;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUserService = currentUserService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
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

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse
        {
            Message = "Đăng ký tài khoản thành công",
            User = userResponse,
            AccessToken = token
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userService.GetByEmailWithPasswordAsync(request.Email, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác");
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Tài khoản của bạn đã bị vô hiệu hóa");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse
        {
            Message = "Đăng nhập thành công",
            User = UserResponse.FromEntity(user),
            AccessToken = token
        };
    }

    public async Task<UserResponse> GetMeAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        return await _userService.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
    }
}
