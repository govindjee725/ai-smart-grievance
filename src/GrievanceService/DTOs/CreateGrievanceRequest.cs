using System.ComponentModel.DataAnnotations;

namespace GrievanceService.DTOs;

public class CreateGrievanceRequest
{
    [Required]
    [StringLength(120, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(5000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [StringLength(250)]
    public string Location { get; set; } = string.Empty;
}