using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs;

public class RegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(72, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}