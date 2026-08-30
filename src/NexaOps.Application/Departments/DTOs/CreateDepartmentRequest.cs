using System.ComponentModel.DataAnnotations;

namespace NexaOps.Application.Departments.DTOs;

public class CreateDepartmentRequest
{
    [Required(ErrorMessage = "Tên phòng ban không được để trống")]
    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ManagerId { get; set; }
    public string? Description { get; set; }
}
