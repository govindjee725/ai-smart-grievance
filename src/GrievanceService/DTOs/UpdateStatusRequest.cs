using System.ComponentModel.DataAnnotations;

namespace GrievanceService.DTOs;

public class UpdateStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Note { get; set; } = string.Empty;

    public string? Priority { get; set; }
}