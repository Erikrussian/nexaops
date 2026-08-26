using System.ComponentModel.DataAnnotations;

namespace NexaOps.Application.Users.DTOs;

public class UserQueryParameters
{
    [Range(1, int.MaxValue, ErrorMessage = "Page tối thiểu là 1")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Limit tối thiểu là 1 và tối đa là 100")]
    public int Limit { get; set; } = 10;

    public string? Search { get; set; }
}
