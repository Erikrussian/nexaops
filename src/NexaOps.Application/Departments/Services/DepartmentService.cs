using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Departments.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Departments.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyAccessService _companyAccessService;

    public DepartmentService(
        IApplicationDbContext context,
        ICompanyAccessService companyAccessService)
    {
        _context = context;
        _companyAccessService = companyAccessService;
    }

    public async Task<DepartmentResponse> CreateAsync(Guid companyId, CreateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageDepartmentsAsync(companyId, cancellationToken);

        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);

        if (company is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy công ty với ID: {companyId}");
        }

        var normalizedName = request.Name.Trim();
        var existingName = await _context.Departments
            .AnyAsync(d => d.CompanyId == companyId && d.Name.ToLower() == normalizedName.ToLower() && d.IsActive, cancellationToken);

        if (existingName)
        {
            throw new InvalidOperationException($"Phòng ban với tên '{request.Name}' đã tồn tại trong công ty");
        }

        Guid? parentId = null;
        if (request.ParentId.HasValue && request.ParentId.Value != Guid.Empty)
        {
            var parent = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == request.ParentId.Value && d.CompanyId == companyId && d.IsActive, cancellationToken);

            if (parent is null)
            {
                throw new KeyNotFoundException("Phòng ban cha (parentId) không tồn tại trong công ty này");
            }
            parentId = parent.Id;
        }

        if (request.ManagerId.HasValue)
        {
            var isValidManager = company.OwnerId == request.ManagerId.Value ||
                await _context.CompanyMembers.AnyAsync(m => m.CompanyId == companyId
                                                         && m.UserId == request.ManagerId.Value
                                                         && m.Status == MemberStatus.Active, cancellationToken);

            if (!isValidManager)
            {
                throw new InvalidOperationException("Người quản lý phòng ban phải là thành viên hợp lệ trong công ty");
            }
        }

        var department = new Department
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = normalizedName,
            Code = request.Code?.Trim(),
            ParentId = parentId,
            ManagerId = request.ManagerId,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync(cancellationToken);

        return DepartmentResponse.FromEntity(department);
    }

    public async Task<object> FindAllByCompanyAsync(Guid companyId, bool tree = false, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);

        var departments = await _context.Departments
            .Include(d => d.Manager)
            .AsNoTracking()
            .Where(d => d.CompanyId == companyId && d.IsActive)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        if (!tree)
        {
            return departments.Select(DepartmentResponse.FromEntity).ToList();
        }

        return BuildDepartmentTree(departments);
    }

    public async Task<DepartmentResponse> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = await _context.Departments
            .Include(d => d.Manager)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (department is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy phòng ban với ID: {id}");
        }

        await _companyAccessService.EnsureReadAccessAsync(department.CompanyId, cancellationToken);

        return DepartmentResponse.FromEntity(department);
    }

    public async Task<DepartmentResponse> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var department = await _context.Departments
            .Include(d => d.Manager)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (department is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy phòng ban với ID: {id}");
        }

        await _companyAccessService.EnsureCanManageDepartmentsAsync(department.CompanyId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var normalizedName = request.Name.Trim();
            if (normalizedName != department.Name)
            {
                var existing = await _context.Departments
                    .AnyAsync(d => d.CompanyId == department.CompanyId
                                && d.Name.ToLower() == normalizedName.ToLower()
                                && d.Id != id
                                && d.IsActive, cancellationToken);

                if (existing)
                {
                    throw new InvalidOperationException($"Tên phòng ban '{request.Name}' đã được sử dụng");
                }

                department.Name = normalizedName;
            }
        }

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == Guid.Empty)
            {
                // NX-BIZ-004: Reparent to root
                department.ParentId = null;
            }
            else
            {
                if (request.ParentId.Value == id)
                {
                    throw new InvalidOperationException("Phòng ban không thể làm cha của chính nó");
                }

                var parent = await _context.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == request.ParentId.Value && d.CompanyId == department.CompanyId && d.IsActive, cancellationToken);

                if (parent is null)
                {
                    throw new KeyNotFoundException("Phòng ban cha (parentId) không tồn tại trong công ty này");
                }

                // NX-BIZ-004: Reject indirect multi-level cycles (e.g. A -> B -> A, A -> B -> C -> A)
                // Traverse upwards from prospective parent to ensure it doesn't lead back to 'id'
                var companyDepts = await _context.Departments
                    .AsNoTracking()
                    .Where(d => d.CompanyId == department.CompanyId && d.IsActive)
                    .Select(d => new { d.Id, d.ParentId })
                    .ToListAsync(cancellationToken);

                var parentMap = companyDepts.ToDictionary(d => d.Id, d => d.ParentId);

                var visited = new HashSet<Guid> { id };
                var curr = request.ParentId.Value;

                while (parentMap.TryGetValue(curr, out var nextParentId) && nextParentId.HasValue)
                {
                    if (visited.Contains(nextParentId.Value))
                    {
                        throw new InvalidOperationException("Không thể thiết lập quan hệ phòng ban cha - con vì sẽ tạo ra chu trình lặp (vòng tròn phân cấp)");
                    }
                    visited.Add(curr);
                    curr = nextParentId.Value;
                }

                department.ParentId = request.ParentId.Value;
            }
        }

        if (request.ManagerId.HasValue)
        {
            var company = await _context.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == department.CompanyId, cancellationToken);
            var isValidManager = (company != null && company.OwnerId == request.ManagerId.Value) ||
                await _context.CompanyMembers.AnyAsync(m => m.CompanyId == department.CompanyId
                                                         && m.UserId == request.ManagerId.Value
                                                         && m.Status == MemberStatus.Active, cancellationToken);

            if (!isValidManager)
            {
                throw new InvalidOperationException("Người quản lý phòng ban phải là thành viên hợp lệ trong công ty");
            }

            department.ManagerId = request.ManagerId.Value;
        }

        if (request.Code is not null)
        {
            department.Code = request.Code.Trim();
        }

        if (request.Description is not null)
        {
            department.Description = request.Description.Trim();
        }

        if (request.IsActive.HasValue)
        {
            department.IsActive = request.IsActive.Value;
        }

        department.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return DepartmentResponse.FromEntity(department);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (department is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy phòng ban với ID: {id}");
        }

        await _companyAccessService.EnsureCanDeleteDepartmentsAsync(department.CompanyId, cancellationToken);

        var hasChildren = await _context.Departments
            .AnyAsync(d => d.ParentId == id && d.IsActive, cancellationToken);

        if (hasChildren)
        {
            throw new InvalidOperationException("Không thể xóa phòng ban này vì đang có các phòng ban con trực thuộc");
        }

        _context.Departments.Remove(department);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static List<DepartmentTreeNodeResponse> BuildDepartmentTree(List<Department> departments)
    {
        var nodeMap = new Dictionary<Guid, DepartmentTreeNodeResponse>();
        var parentMap = new Dictionary<Guid, Guid>();
        var rootNodes = new List<DepartmentTreeNodeResponse>();

        foreach (var dept in departments)
        {
            nodeMap[dept.Id] = DepartmentTreeNodeResponse.FromEntity(dept);
            if (dept.ParentId.HasValue)
            {
                parentMap[dept.Id] = dept.ParentId.Value;
            }
        }

        foreach (var dept in departments)
        {
            var node = nodeMap[dept.Id];
            if (dept.ParentId.HasValue && nodeMap.TryGetValue(dept.ParentId.Value, out var parentNode))
            {
                // NX-BIZ-004: Detect and break cycles in corrupted historical data to avoid infinite traversal / JSON recursion
                var curr = parentNode.Id;
                var hasCycle = false;
                var visited = new HashSet<Guid> { node.Id };

                while (curr != Guid.Empty)
                {
                    if (visited.Contains(curr))
                    {
                        hasCycle = true;
                        break;
                    }
                    visited.Add(curr);
                    if (!parentMap.TryGetValue(curr, out curr))
                    {
                        break;
                    }
                }

                if (!hasCycle)
                {
                    parentNode.Children.Add(node);
                }
                else
                {
                    // Break corrupted cycle by promoting node to root
                    rootNodes.Add(node);
                }
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        return rootNodes;
    }
}
