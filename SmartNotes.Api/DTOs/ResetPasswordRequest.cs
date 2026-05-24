using System.ComponentModel.DataAnnotations;

namespace SmartNotes.Api.DTOs;

public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(24, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
