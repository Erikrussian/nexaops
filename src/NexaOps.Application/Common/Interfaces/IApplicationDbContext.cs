using Microsoft.EntityFrameworkCore;
using NexaOps.Domain.Entities;

namespace NexaOps.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Company> Companies { get; }
    DbSet<Department> Departments { get; }
    DbSet<CompanyMember> CompanyMembers { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
