namespace SmartNotes.Api.Models
{
    public class Note
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string JobId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? ClassroomId { get; set; }
        public bool IsPublic { get; set; } = false; // Per si volem compartir la nota amb altres usuaris (Funcionalitat futura)
        public string? PublicId { get; set; } // Un identificador públic únic per compartir la nota (Funcionalitat futura)
    }
}