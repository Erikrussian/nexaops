using System.ComponentModel.DataAnnotations;

namespace NexaOps.Application.Companies.DTOs;

public class CreateCompanyRequest
{
    [Required(ErrorMessage = "Tên công ty không được để trống")]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Code { get; set; }
    public string? Logo { get; set; }
    public string? Description { get; set; }
}
