using System.ComponentModel.DataAnnotations;

namespace SmartNotes.Api.DTOs
{
    public class RefreshRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}