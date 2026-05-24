using System.ComponentModel.DataAnnotations;

namespace SmartNotes.Api.DTOs
{
    public class RegisterRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(24, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        public string? Role { get; set; }
        public List<string>? Languages { get; set; }
    }
}