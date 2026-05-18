namespace SmartNotes.Api.Models;

public class TranscriptionRecord
{
    public int Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public int UserId { get; set; } = default!;
    public User User { get; set; } = null!;
    public string OriginalFileName { get; set; } = default!;
    public string CleanText { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? EnhancedAudioPath { get; set; }
}