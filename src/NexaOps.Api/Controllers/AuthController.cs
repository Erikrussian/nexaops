using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Auth.Services;
using NexaOps.Application.Users.DTOs;

namespace NexaOps.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// POST /auth/register - Đăng ký tài khoản mới và thiết lập phiên
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// POST /auth/login - Đăng nhập tài khoản và tạo phiên làm việc
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(result);
    }

    /// <summary>
    /// POST /auth/refresh - Làm mới access token thông qua refresh token (Rotation + Anomaly check)
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> RefreshToken(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var token = GetRefreshTokenFromRequest(request);

        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Refresh token không tồn tại trong cookie hoặc request body",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var result = await _authService.RefreshTokenAsync(
            token,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(result);
    }

    /// <summary>
    /// POST /auth/logout - Đăng xuất và thu hồi phiên hiện tại
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Logout(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var token = GetRefreshTokenFromRequest(request);

        await _authService.LogoutAsync(
            token,
            GetIpAddress(),
            cancellationToken);

        ClearRefreshTokenCookie();

        return Ok(new { message = "Đăng xuất thành công" });
    }

    /// <summary>
    /// POST /auth/logout-all - Thu hồi toàn bộ các phiên làm việc của người dùng
    /// </summary>
    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await _authService.LogoutAllAsync(cancellationToken);
        ClearRefreshTokenCookie();
        return Ok(new { message = "Đã thu hồi toàn bộ phiên làm việc của tài khoản" });
    }

    /// <summary>
    /// GET /auth/sessions - Lấy danh sách các phiên làm việc đang hoạt động của người dùng
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<UserSessionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UserSessionResponse>>> GetSessions(CancellationToken cancellationToken)
    {
        var currentToken = GetRefreshTokenFromRequest(null);
        var sessions = await _authService.GetActiveSessionsAsync(currentToken, cancellationToken);
        return Ok(sessions);
    }

    /// <summary>
    /// DELETE /auth/sessions/{sessionId} - Thu hồi một phiên làm việc cụ thể
    /// </summary>
    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeSession(
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeSessionAsync(sessionId, cancellationToken);
        return Ok(new { message = "Đã thu hồi phiên làm việc thành công" });
    }

    /// <summary>
    /// GET /auth/me - Lấy thông tin tài khoản hiện tại từ JWT Bearer Token
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken cancellationToken)
    {
        var result = await _authService.GetMeAsync(cancellationToken);
        return Ok(result);
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(7),
            Path = "/"
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            Path = "/"
        });
    }

    private string? GetRefreshTokenFromRequest(RefreshTokenRequest? bodyRequest)
    {
        if (!string.IsNullOrWhiteSpace(bodyRequest?.RefreshToken))
        {
            return bodyRequest.RefreshToken;
        }

        if (Request.Cookies.TryGetValue("refreshToken", out var cookieToken) && !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken;
        }

        return null;
    }

    private string? GetIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var ip) && !string.IsNullOrWhiteSpace(ip))
        {
            return ip.ToString().Split(',')[0].Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent() => Request.Headers.UserAgent.ToString();
}
