using Microsoft.EntityFrameworkCore;
using NexaOps.Domain.Entities;

namespace NexaOps.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
