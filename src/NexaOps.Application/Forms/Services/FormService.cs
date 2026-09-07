using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Forms.Services;

public interface IFormService
{
    Task<FormDefinitionDto> CreateAsync(Guid companyId, CreateFormDto dto, CancellationToken cancellationToken = default);
    Task<FormDefinitionDto> GetByIdAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default);
    Task<FormDefinitionDto> GetByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task<List<FormDefinitionDto>> ListAsync(Guid companyId, Guid? departmentId = null, FormStatus? status = null, CancellationToken cancellationToken = default);
    Task<FormDefinitionDto> UpdateAsync(Guid companyId, Guid formId, UpdateFormDto dto, CancellationToken cancellationToken = default);
    Task<FormDefinitionDto> ChangeStatusAsync(Guid companyId, Guid formId, FormStatus newStatus, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default);
}

public class FormService : IFormService
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyAccessService _companyAccessService;
    private readonly ICurrentUserService _currentUserService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FormService(
        IApplicationDbContext context,
        ICompanyAccessService companyAccessService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _companyAccessService = companyAccessService;
        _currentUserService = currentUserService;
    }

    public async Task<FormDefinitionDto> CreateAsync(Guid companyId, CreateFormDto dto, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageFormsAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new ArgumentException("Tiêu đề biểu mẫu không được để trống", nameof(dto.Title));
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            throw new ArgumentException("Mã biểu mẫu không được để trống", nameof(dto.Code));
        }

        var normalizedCode = dto.Code.Trim().ToUpperInvariant();

        var codeExists = await _context.FormDefinitions
            .AnyAsync(f => f.CompanyId == companyId && f.Code == normalizedCode, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"Mã biểu mẫu '{normalizedCode}' đã tồn tại trong công ty này");
        }

        if (dto.DepartmentId.HasValue)
        {
            var deptExists = await _context.Departments
                .AnyAsync(d => d.Id == dto.DepartmentId.Value && d.CompanyId == companyId, cancellationToken);

            if (!deptExists)
            {
                throw new ArgumentException("Phòng ban không tồn tại trong công ty này", nameof(dto.DepartmentId));
            }
        }

        var form = new FormDefinition
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DepartmentId = dto.DepartmentId,
            Title = dto.Title.Trim(),
            Code = normalizedCode,
            Description = dto.Description?.Trim(),
            Status = FormStatus.Draft,
            Version = 1,
            SchemaJson = JsonSerializer.Serialize(dto.Fields ?? new List<FormFieldDto>(), JsonOptions),
            CreatedById = currentUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.FormDefinitions.Add(form);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(companyId, form.Id, cancellationToken);
    }

    public async Task<FormDefinitionDto> GetByIdAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);

        var form = await _context.FormDefinitions
            .Include(f => f.Department)
            .Include(f => f.CreatedBy)
            .FirstOrDefaultAsync(f => f.Id == formId && f.CompanyId == companyId, cancellationToken);

        if (form == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy biểu mẫu với ID: {formId}");
        }

        return MapToDto(form);
    }

    public async Task<FormDefinitionDto> GetByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);

        var normalizedCode = code.Trim().ToUpperInvariant();

        var form = await _context.FormDefinitions
            .Include(f => f.Department)
            .Include(f => f.CreatedBy)
            .FirstOrDefaultAsync(f => f.Code == normalizedCode && f.CompanyId == companyId, cancellationToken);

        if (form == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy biểu mẫu với mã: {code}");
        }

        return MapToDto(form);
    }

    public async Task<List<FormDefinitionDto>> ListAsync(Guid companyId, Guid? departmentId = null, FormStatus? status = null, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);

        var query = _context.FormDefinitions
            .Include(f => f.Department)
            .Include(f => f.CreatedBy)
            .Where(f => f.CompanyId == companyId);

        if (departmentId.HasValue)
        {
            query = query.Where(f => f.DepartmentId == departmentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(f => f.Status == status.Value);
        }

        var forms = await query
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return forms.Select(MapToDto).ToList();
    }

    public async Task<FormDefinitionDto> UpdateAsync(Guid companyId, Guid formId, UpdateFormDto dto, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageFormsAsync(companyId, cancellationToken);

        var form = await _context.FormDefinitions
            .FirstOrDefaultAsync(f => f.Id == formId && f.CompanyId == companyId, cancellationToken);

        if (form == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy biểu mẫu với ID: {formId}");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new ArgumentException("Tiêu đề biểu mẫu không được để trống", nameof(dto.Title));
        }

        if (dto.DepartmentId.HasValue)
        {
            var deptExists = await _context.Departments
                .AnyAsync(d => d.Id == dto.DepartmentId.Value && d.CompanyId == companyId, cancellationToken);

            if (!deptExists)
            {
                throw new ArgumentException("Phòng ban không tồn tại trong công ty này", nameof(dto.DepartmentId));
            }
        }

        form.Title = dto.Title.Trim();
        form.Description = dto.Description?.Trim();
        form.DepartmentId = dto.DepartmentId;

        if (dto.Fields != null)
        {
            form.SchemaJson = JsonSerializer.Serialize(dto.Fields, JsonOptions);
            form.Version++;
        }

        form.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(companyId, form.Id, cancellationToken);
    }

    public async Task<FormDefinitionDto> ChangeStatusAsync(Guid companyId, Guid formId, FormStatus newStatus, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageFormsAsync(companyId, cancellationToken);

        var form = await _context.FormDefinitions
            .FirstOrDefaultAsync(f => f.Id == formId && f.CompanyId == companyId, cancellationToken);

        if (form == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy biểu mẫu với ID: {formId}");
        }

        form.Status = newStatus;
        form.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(companyId, form.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageFormsAsync(companyId, cancellationToken);

        var form = await _context.FormDefinitions
            .FirstOrDefaultAsync(f => f.Id == formId && f.CompanyId == companyId, cancellationToken);

        if (form == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy biểu mẫu với ID: {formId}");
        }

        var hasSubmissions = await _context.FormSubmissions
            .AnyAsync(s => s.FormDefinitionId == formId, cancellationToken);

        if (hasSubmissions)
        {
            throw new InvalidOperationException("Không thể xóa biểu mẫu đã có bài nộp. Hãy chuyển sang trạng thái Archived.");
        }

        _context.FormDefinitions.Remove(form);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static FormDefinitionDto MapToDto(FormDefinition form)
    {
        List<FormFieldDto> fields;
        try
        {
            fields = JsonSerializer.Deserialize<List<FormFieldDto>>(form.SchemaJson, JsonOptions) ?? new List<FormFieldDto>();
        }
        catch
        {
            fields = new List<FormFieldDto>();
        }

        return new FormDefinitionDto
        {
            Id = form.Id,
            CompanyId = form.CompanyId,
            DepartmentId = form.DepartmentId,
            DepartmentName = form.Department?.Name,
            Title = form.Title,
            Code = form.Code,
            Description = form.Description,
            Status = form.Status,
            Version = form.Version,
            Fields = fields,
            CreatedById = form.CreatedById,
            CreatedByName = form.CreatedBy?.Name ?? form.CreatedBy?.Email,
            CreatedAt = form.CreatedAt,
            UpdatedAt = form.UpdatedAt
        };
    }
}
