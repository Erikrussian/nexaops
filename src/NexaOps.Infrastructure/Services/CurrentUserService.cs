using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NexaOps.Application.Common.Interfaces;

namespace NexaOps.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var subClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? user?.FindFirst("sub")?.Value;

            return Guid.TryParse(subClaim, out var parsedGuid) ? parsedGuid : null;
        }
    }

    public string? Email
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.Email)?.Value
                ?? user?.FindFirst("email")?.Value;
        }
    }

    public string? Role
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.Role)?.Value
                ?? user?.FindFirst("role")?.Value;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
