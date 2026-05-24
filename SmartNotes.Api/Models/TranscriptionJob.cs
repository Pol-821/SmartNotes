using System.Text.Json.Serialization;
using System.Threading;
namespace SmartNotes.Api.Models;
public class TranscriptionJob : IDisposable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public TranscriptionStatus Status { get; set; } = TranscriptionStatus.Pending;
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FilePath { get; set; }
    [JsonIgnore]
    public CancellationTokenSource Cancellation {get; set; } = new();
    public int? UserId { get; set; }
    public string? OriginalFileName { get; set; }
    public string? Summary { get; set; }
    public bool IsRetry { get; set; } = false;
    public string? ForcedLanguage { get; set; }
    public string? ProgressMessage { get; set; }
    public int ProgressPercentage { get; set; } = 0;
    public string? AudioDuration { get; set; }

    public void UpdateProgress(string message, int percentage)
    {
        ProgressMessage = message;
        ProgressPercentage = Math.Min(100, percentage);
    }

    public void Dispose()
    {
        Cancellation?.Cancel();
        Cancellation?.Dispose();
    }
}
