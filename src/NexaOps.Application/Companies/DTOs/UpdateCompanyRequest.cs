namespace NexaOps.Application.Companies.DTOs;

public class UpdateCompanyRequest
{
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public string? Code { get; set; }
    public string? Logo { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
