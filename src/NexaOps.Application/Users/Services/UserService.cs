using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Common.Models;
using NexaOps.Application.Users.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Users.Services;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;

    public UserService(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
    }

    private void EnsureIsAuthenticated()
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }
    }

    private bool IsSystemAdmin()
    {
        return string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (_currentUserService.IsAuthenticated && !IsSystemAdmin())
        {
            throw new ForbiddenAccessException("Chỉ Quản trị viên hệ thống mới có quyền tạo người dùng trực tiếp");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException("Email này đã được sử dụng");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Role = request.Role ?? UserRole.User,
            Avatar = request.Avatar,
            Phone = request.Phone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return UserResponse.FromEntity(user);
    }

    public async Task<PagedResult<UserResponse>> GetAllAsync(UserQueryParameters query, CancellationToken cancellationToken = default)
    {
        EnsureIsAuthenticated();

        if (!IsSystemAdmin())
        {
            throw new ForbiddenAccessException("Chỉ Quản trị viên hệ thống mới có quyền xem toàn bộ danh sách người dùng");
        }

        var queryable = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            queryable = queryable.Where(u => u.Name.ToLower().Contains(search) || u.Email.ToLower().Contains(search));
        }

        var total = await queryable.CountAsync(cancellationToken);
        var skip = (query.Page - 1) * query.Limit;

        var items = await queryable
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Take(query.Limit)
            .Select(u => UserResponse.FromEntity(u))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserResponse>
        {
            Items = items,
            Meta = new PageMetadata
            {
                Total = total,
                Page = query.Page,
                Limit = query.Limit,
                TotalPages = (int)Math.Ceiling((double)total / query.Limit)
            }
        };
    }

    public async Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureIsAuthenticated();

        if (!IsSystemAdmin() && _currentUserService.UserId != id)
        {
            throw new ForbiddenAccessException("Bạn chỉ có quyền xem thông tin của chính mình");
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy người dùng với ID: {id}");
        }

        return UserResponse.FromEntity(user);
    }

    public async Task<User?> GetByEmailWithPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIsAuthenticated();

        if (!IsSystemAdmin() && _currentUserService.UserId != id)
        {
            throw new ForbiddenAccessException("Bạn chỉ có quyền cập nhật thông tin của chính mình");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy người dùng với ID: {id}");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (normalizedEmail != user.Email)
            {
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email == normalizedEmail && u.Id != id, cancellationToken);

                if (emailExists)
                {
                    throw new InvalidOperationException("Email này đã được sử dụng bởi người khác");
                }

                user.Email = normalizedEmail;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            user.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = _passwordHasher.HashPassword(request.Password);
        }

        if (request.Role.HasValue)
        {
            if (!IsSystemAdmin())
            {
                throw new ForbiddenAccessException("Chỉ Quản trị viên hệ thống mới có quyền thay đổi vai trò người dùng");
            }
            user.Role = request.Role.Value;
        }

        if (request.Avatar is not null)
        {
            user.Avatar = request.Avatar;
        }

        if (request.Phone is not null)
        {
            user.Phone = request.Phone;
        }

        if (request.IsActive.HasValue)
        {
            if (!IsSystemAdmin())
            {
                throw new ForbiddenAccessException("Chỉ Quản trị viên hệ thống mới có quyền kích hoạt hoặc vô hiệu hóa tài khoản");
            }
            user.IsActive = request.IsActive.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return UserResponse.FromEntity(user);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureIsAuthenticated();

        if (!IsSystemAdmin())
        {
            throw new ForbiddenAccessException("Chỉ Quản trị viên hệ thống mới có quyền xóa người dùng");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy người dùng với ID: {id}");
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
