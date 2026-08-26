using NexaOps.Application.Common.Models;
using NexaOps.Application.Users.DTOs;
using NexaOps.Domain.Entities;

namespace NexaOps.Application.Users.Services;

public interface IUserService
{
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<UserResponse>> GetAllAsync(UserQueryParameters query, CancellationToken cancellationToken = default);
    Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailWithPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
