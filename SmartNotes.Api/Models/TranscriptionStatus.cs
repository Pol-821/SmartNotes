namespace SmartNotes.Api.Models;

public enum TranscriptionStatus
{
    Pending,
    AudioCleaning,
    DetectingLanguage,
    Transcribing,
    Summarizing,
    Done,
    Error,
    Cancelled
}
