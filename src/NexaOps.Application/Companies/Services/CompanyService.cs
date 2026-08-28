using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Domain.Entities;

namespace NexaOps.Application.Companies.Services;

public class CompanyService : ICompanyService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CompanyService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<CompanyResponse> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        var slug = !string.IsNullOrWhiteSpace(request.Slug)
            ? request.Slug.Trim().ToLowerInvariant()
            : GenerateSlug(request.Name);

        var existing = await _context.Companies
            .AnyAsync(c => c.Slug == slug, cancellationToken);

        if (existing)
        {
            throw new InvalidOperationException($"Slug công ty '{slug}' đã được sử dụng");
        }

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Code = request.Code?.Trim(),
            Logo = request.Logo?.Trim(),
            Description = request.Description?.Trim(),
            OwnerId = _currentUserService.UserId.Value,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync(cancellationToken);

        return CompanyResponse.FromEntity(company);
    }

    public async Task<IReadOnlyList<CompanyResponse>> FindMyCompaniesAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        var companies = await _context.Companies
            .AsNoTracking()
            .Where(c => c.OwnerId == _currentUserService.UserId.Value && c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => CompanyResponse.FromEntity(c))
            .ToListAsync(cancellationToken);

        return companies;
    }

    public async Task<CompanyResponse> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy công ty với ID: {id}");
        }

        return CompanyResponse.FromEntity(company);
    }

    public async Task<CompanyResponse> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == normalizedSlug, cancellationToken);

        if (company is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy công ty với slug: {slug}");
        }

        return CompanyResponse.FromEntity(company);
    }

    public async Task<CompanyResponse> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy công ty với ID: {id}");
        }

        if (company.OwnerId != _currentUserService.UserId.Value)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa thông tin công ty này");
        }

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            var newSlug = request.Slug.Trim().ToLowerInvariant();
            if (newSlug != company.Slug)
            {
                var existing = await _context.Companies
                    .AnyAsync(c => c.Slug == newSlug && c.Id != id, cancellationToken);

                if (existing)
                {
                    throw new InvalidOperationException($"Slug '{newSlug}' đã được sử dụng");
                }

                company.Slug = newSlug;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            company.Name = request.Name.Trim();
        }

        if (request.Code is not null)
        {
            company.Code = request.Code.Trim();
        }

        if (request.Logo is not null)
        {
            company.Logo = request.Logo.Trim();
        }

        if (request.Description is not null)
        {
            company.Description = request.Description.Trim();
        }

        if (request.IsActive.HasValue)
        {
            company.IsActive = request.IsActive.Value;
        }

        company.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return CompanyResponse.FromEntity(company);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy công ty với ID: {id}");
        }

        if (company.OwnerId != _currentUserService.UserId.Value)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xóa công ty này");
        }

        _context.Companies.Remove(company);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateSlug(string name)
    {
        var normalizedString = name.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var cleanString = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        cleanString = cleanString.Replace('đ', 'd').Replace('Đ', 'd');
        cleanString = Regex.Replace(cleanString, @"[^a-z0-9\s-]", "");
        cleanString = Regex.Replace(cleanString.Trim(), @"\s+", "-");

        var randomSuffix = Random.Shared.Next(1000, 10000);
        return $"{cleanString}-{randomSuffix}";
    }
}
