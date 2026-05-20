using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Services.AI;

public class GroqAudioClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GroqAudioClient> _logger;
    private const string Endpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
    private const long MaxChunkSizeBytes = 18L * 1024 * 1024;

    public GroqAudioClient(string apiKey, ILogger<GroqAudioClient> logger)
    {
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromMinutes(5);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(string audioPath, string language, CancellationToken ct)
    {
        return await TranscribeWithProgressAsync(audioPath, language, ct, null, 0);
    }

    public async Task<string> TranscribeWithProgressAsync(string audioPath, string language, CancellationToken ct, TranscriptionJob? job, double totalDuration)
    {
        var fileSize = new FileInfo(audioPath).Length;

        if (fileSize <= MaxChunkSizeBytes)
        {
            var result = await TranscribeSingleFileAsync(audioPath, language, ct);
            job?.UpdateProgress("Transcripció completada", 65);
            return result;
        }

        _logger.LogInformation("Audio file {SizeMB} MB exceeds limit. Splitting into chunks...", fileSize / (1024 * 1024));

        var audioDir = Path.GetDirectoryName(audioPath) ?? Path.GetTempPath();
        var baseName = Path.GetFileNameWithoutExtension(audioPath);
        var chunkDir = Path.Combine(audioDir, $"{baseName}_chunks");

        if (!Directory.Exists(chunkDir)) Directory.CreateDirectory(chunkDir);

        try
        {
            await SplitAudioIntoChunksAsync(audioPath, chunkDir, ct);

            var chunkFiles = Directory.GetFiles(chunkDir)
                .OrderBy(f => f)
                .ToList();

            _logger.LogInformation("{Count} chunks created. Transcribing...", chunkFiles.Count);

            var fullTranscript = new List<string>();
            var totalChunks = chunkFiles.Count;

            for (int i = 0; i < chunkFiles.Count; i++)
            {
                if (i > 0) await Task.Delay(TimeSpan.FromSeconds(3), ct);
                var chunk = chunkFiles[i];
                _logger.LogInformation("Transcribing chunk {Current}/{Total}...", i + 1, chunkFiles.Count);

                var currentAudioMinute = (totalDuration / totalChunks) * i;
                var currentFormatted = TimeSpan.FromSeconds(currentAudioMinute).ToString(@"hh\:mm\:ss");
                var totalFormatted = TimeSpan.FromSeconds(totalDuration).ToString(@"hh\:mm\:ss");

                if (job != null)
                {
                    job.ProgressMessage = $"Transcrivint... {currentFormatted} / {totalFormatted} (chunk {i + 1}/{totalChunks})";
                    var transcriptionProgress = 40 + (int)((double)i / totalChunks * 30);
                    job.ProgressPercentage = Math.Max(job.ProgressPercentage, transcriptionProgress);
                }

                var result = await TranscribeSingleFileAsync(chunk, language, ct);
                fullTranscript.Add(result);
            }

            job?.UpdateProgress("Transcripció completada", 65);
            return string.Join(" ", fullTranscript).Trim();
        }
        finally
        {
            if (Directory.Exists(chunkDir))
            {
                try { Directory.Delete(chunkDir, true); }
                catch { }
            }
        }
    }

    public async Task<string> DetectLanguageAsync(string audioPath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
            return "";

        var sampleDir = Path.GetDirectoryName(audioPath) ?? Path.GetTempPath();
        var samplePath = Path.Combine(sampleDir, "lang_sample.mp3");

        if (File.Exists(samplePath)) File.Delete(samplePath);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-nostdin -y -i \"{audioPath}\" -t 30 -ar 16000 -ac 1 \"{samplePath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new System.Diagnostics.Process { StartInfo = psi };
            process.Start();
            process.StandardError.ReadToEnd();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || !File.Exists(samplePath))
                return "";

            var text = await TranscribeSingleFileAsync(samplePath, "auto", ct);

            if (string.IsNullOrWhiteSpace(text))
                return "";

            var lang = await DetectLanguageFromTextAsync(text, ct);
            return lang;
        }
        finally
        {
            if (File.Exists(samplePath)) File.Delete(samplePath);
        }
    }

    private async Task<string> TranscribeSingleFileAsync(string audioPath, string language, CancellationToken ct)
    {
        int maxRetries = 5;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(audioPath, ct);
                using var fileContent = new ByteArrayContent(fileBytes);

                var ext = Path.GetExtension(audioPath).ToLowerInvariant();
                var mimeType = ext switch
                {
                    ".wav" => "audio/wav",
                    ".mp3" => "audio/mpeg",
                    ".m4a" => "audio/mp4",
                    ".flac" => "audio/flac",
                    _ => "audio/mpeg"
                };
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

                using var form = new MultipartFormDataContent();
                form.Add(fileContent, "file", Path.GetFileName(audioPath));
                
                if (!string.IsNullOrEmpty(language) && language != "auto")
                {
                    form.Add(new StringContent(language), "language");
                }
                
                form.Add(new StringContent("whisper-large-v3"), "model");
                form.Add(new StringContent("text"), "response_format");

                response = await _http.PostAsync(Endpoint, form, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                
                if ((int)response.StatusCode == 429)
                {
                    var backoff = 10 * (attempt + 1);
                    if (attempt == maxRetries - 1)
                    {
                        _logger.LogError("Rate limited (429) on chunk transcription after {MaxRetries} retries", maxRetries);
                        throw new HttpRequestException("Rate limited after retries");
                    }
                    _logger.LogWarning("Rate limited (429). Waiting {Backoff}s before retry", backoff);
                    await Task.Delay(TimeSpan.FromSeconds(backoff), ct);
                    continue;
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Groq returned {StatusCode}: {ErrorBody}", (int)response.StatusCode, responseBody);
                    throw new HttpRequestException($"Groq {response.StatusCode}: {responseBody}");
                }
                
                return responseBody.Trim();
            }
            catch (Exception ex) when (attempt < maxRetries - 1 && ex is not HttpRequestException)
            {
                var backoff = 5 * (attempt + 1);
                _logger.LogWarning(ex, "Chunk transcription attempt {Attempt}/{MaxRetries} failed, retrying in {Backoff}s...", attempt + 1, maxRetries, backoff);
                await Task.Delay(TimeSpan.FromSeconds(backoff), ct);
            }
            finally
            {
                response?.Dispose();
            }
        }
        throw new HttpRequestException($"Chunk transcription failed after {maxRetries} attempts");
    }

    private async Task<string> DetectLanguageFromTextAsync(string text, CancellationToken ct)
    {
        var prompt = $@"Detect the language of this text. Respond with ONLY the 2-letter language code (e.g. 'ca', 'es', 'en', 'fr'). Nothing else.

Text: {text.Substring(0, Math.Min(500, text.Length))}";

        var payload = new
        {
            model = "llama-3.3-70b-versatile",
            messages = new[]
            {
                new { role = "system", content = "You are a language detector. Respond with only a 2-letter ISO language code." },
                new { role = "user", content = prompt }
            },
            max_tokens = 10,
            temperature = 0
        };

        try
        {
            var response = await _http.PostAsJsonAsync("https://api.groq.com/openai/v1/chat/completions", payload, ct);
            if (!response.IsSuccessStatusCode) return "";

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim().ToLower() ?? "";

            return content.Length >= 2 ? content.Substring(0, 2) : "";
        }
        catch
        {
            return "";
        }
    }

    private async Task SplitAudioIntoChunksAsync(string audioPath, string outputDir, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-nostdin -y -i \"{audioPath}\" -f segment -segment_time 300 -c:a libmp3lame -b:a 128k \"{outputDir}/chunk_%03d.mp3\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();
        process.StandardError.ReadToEnd();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new Exception($"FFmpeg ha fallat al dividir l'àudio. ExitCode: {process.ExitCode}");
        }
    }
}
