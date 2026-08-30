using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.Departments.DTOs;
using NexaOps.Application.Departments.Services;

namespace NexaOps.Api.Controllers;

[ApiController]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    /// <summary>
    /// POST /companies/{companyId}/departments - Tạo phòng ban mới trong công ty
    /// </summary>
    [HttpPost("companies/{companyId:guid}/departments")]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentResponse>> Create(
        [FromRoute] Guid companyId,
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _departmentService.CreateAsync(companyId, request, cancellationToken);
        return CreatedAtAction(nameof(FindById), new { id = result.Id }, result);
    }

    /// <summary>
    /// GET /companies/{companyId}/departments - Lấy danh sách phòng ban (phẳng hoặc cây phân cấp)
    /// </summary>
    [HttpGet("companies/{companyId:guid}/departments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> FindAllByCompany(
        [FromRoute] Guid companyId,
        [FromQuery] bool tree = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _departmentService.FindAllByCompanyAsync(companyId, tree, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /departments/{id} - Lấy chi tiết phòng ban theo ID
    /// </summary>
    [HttpGet("departments/{id:guid}")]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentResponse>> FindById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _departmentService.FindByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PATCH /departments/{id} - Cập nhật thông tin phòng ban
    /// </summary>
    [HttpPatch("departments/{id:guid}")]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _departmentService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /departments/{id} - Xóa phòng ban (Chặn nếu có phòng ban con)
    /// </summary>
    [HttpDelete("departments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Remove(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _departmentService.RemoveAsync(id, cancellationToken);
        return Ok(new { message = "Đã xóa phòng ban thành công" });
    }
}
