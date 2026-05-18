namespace SmartNotes.Api.DTOs;

public class TranscriptionJobDto
{
    public string Id { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Result { get; set; }
    public string? Summary { get; set; }
    public string? ErrorMessage { get; set; }
}