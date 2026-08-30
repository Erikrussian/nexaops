namespace NexaOps.Application.Departments.DTOs;

public class UpdateDepartmentRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ManagerId { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
