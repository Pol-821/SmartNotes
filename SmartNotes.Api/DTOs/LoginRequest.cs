using System.ComponentModel.DataAnnotations;

namespace SmartNotes.Api.DTOs
{
    public class LoginRequest
    {
        [Required]
        public string Identifier { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}