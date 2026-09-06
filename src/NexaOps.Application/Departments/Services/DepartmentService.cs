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

        if (request.ParentId.HasValue)
        {
            var parent = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == request.ParentId.Value && d.CompanyId == companyId, cancellationToken);

            if (parent is null)
            {
                throw new KeyNotFoundException("Phòng ban cha (parentId) không tồn tại trong công ty này");
            }
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
            ParentId = request.ParentId,
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
            if (request.ParentId.Value == id)
            {
                throw new InvalidOperationException("Phòng ban không thể làm cha của chính nó");
            }

            var parentExists = await _context.Departments
                .AnyAsync(d => d.Id == request.ParentId.Value && d.CompanyId == department.CompanyId, cancellationToken);

            if (!parentExists)
            {
                throw new KeyNotFoundException("Phòng ban cha (parentId) không tồn tại trong công ty này");
            }

            department.ParentId = request.ParentId.Value;
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
        var rootNodes = new List<DepartmentTreeNodeResponse>();

        foreach (var dept in departments)
        {
            nodeMap[dept.Id] = DepartmentTreeNodeResponse.FromEntity(dept);
        }

        foreach (var dept in departments)
        {
            var node = nodeMap[dept.Id];
            if (dept.ParentId.HasValue && nodeMap.TryGetValue(dept.ParentId.Value, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        return rootNodes;
    }
}
