using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.Companies.Services;

namespace NexaOps.Api.Controllers;

[ApiController]
[Route("companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    /// <summary>
    /// POST /companies - Tạo công ty mới
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CompanyResponse>> Create(
        [FromBody] CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(FindById), new { id = result.Id }, result);
    }

    /// <summary>
    /// GET /companies - Lấy danh sách các công ty của user hiện tại
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> FindMyCompanies(CancellationToken cancellationToken)
    {
        var result = await _companyService.FindMyCompaniesAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/slug/{slug} - Tìm công ty theo Slug
    /// </summary>
    [HttpGet("slug/{slug}")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> FindBySlug(
        [FromRoute] string slug,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.FindBySlugAsync(slug, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{id} - Tìm công ty theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> FindById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.FindByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PATCH /companies/{id} - Cập nhật thông tin công ty (Chỉ Owner)
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /companies/{id}/transfer-ownership - Chuyển nhượng quyền sở hữu công ty (Chỉ Owner hiện tại)
    /// </summary>
    [HttpPost("{id:guid}/transfer-ownership")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> TransferOwnership(
        [FromRoute] Guid id,
        [FromBody] TransferOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.TransferOwnershipAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /companies/{id} - Xóa công ty (Chỉ Owner)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Remove(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _companyService.RemoveAsync(id, cancellationToken);
        return Ok(new { message = "Đã xóa công ty thành công" });
    }
}
