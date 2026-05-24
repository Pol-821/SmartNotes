using System.ComponentModel.DataAnnotations;

namespace SmartNotes.Api.DTOs
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}