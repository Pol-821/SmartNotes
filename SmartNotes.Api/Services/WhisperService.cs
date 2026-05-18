using SmartNotes.Api.Services.AI;

namespace SmartNotes.Api.Services;

public class WhisperService
{
    private readonly GroqAudioClient _groqAudio;

    public WhisperService(GroqAudioClient groqAudio)
    {
        _groqAudio = groqAudio;
    }

    public async Task<string> TranscribeAsync(string audioPath, CancellationToken ct, string targetLanguage)
    {
        Console.WriteLine($"[GROQ AUDIO] Transcrivint en idioma '{targetLanguage}'...");
        var result = await _groqAudio.TranscribeAsync(audioPath, targetLanguage, ct);
        Console.WriteLine($"[GROQ AUDIO] Transcripció completada. {result.Length} caràcters obtinguts.");
        return result;
    }

    public async Task<string> TranscribeWithProgressAsync(string audioPath, SmartNotes.Api.Models.TranscriptionJob job, string targetLanguage, CancellationToken ct, double totalDuration)
    {
        Console.WriteLine($"[GROQ AUDIO] Transcrivint en idioma '{targetLanguage}'...");
        var result = await _groqAudio.TranscribeWithProgressAsync(audioPath, targetLanguage, ct, job, totalDuration);
        Console.WriteLine($"[GROQ AUDIO] Transcripció completada. {result.Length} caràcters obtinguts.");
        return result;
    }

    public async Task<string> DetectLanguageAsync(string audioPath, CancellationToken ct)
    {
        Console.WriteLine("[GROQ AUDIO] Detectant idioma...");
        var lang = await _groqAudio.DetectLanguageAsync(audioPath, ct);
        Console.WriteLine($"[GROQ AUDIO] Idioma detectat: '{lang}'");
        return lang;
    }
}
