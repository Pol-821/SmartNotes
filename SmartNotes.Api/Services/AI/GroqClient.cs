using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SmartNotes.Api.Services.AI;

public class GroqClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GroqClient> _logger;
    private readonly string _model;

    public GroqClient(string apiKey, string model, ILogger<GroqClient> logger)
    {
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromMinutes(2);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _model = model;
        _logger = logger;
    }

    public async Task<string> ChatAsync(string prompt, string idiomaDetectat = "es")
    {
        var langName = idiomaDetectat switch
        {
            "es" => "Spanish",
            "ca" => "Catalan",
            "en" => "English",
            "fr" => "French",
            _ => idiomaDetectat
        };

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new {
                    role = "system",
                    content = $@"You are a world-class academic secretary. Your goal is to transform messy transcripts into high-quality, professional study notes.
                    STRICT JSON RULES:
                    - You must respond ONLY with a JSON object.
                    - Keys: ""Titol"", ""ResumGeneral"", ""Subtemes"", ""TasquesProfessor"", ""PropostaEstudi"".
                    - NEVER translate keys.

                    CONTENT RULES:
                    - LANGUAGE: ALL content values MUST be written in {langName}. This is the language of the original transcript. Do NOT use any other language.
                    - NO GENERALIZATIONS: Do not say 'The teacher explains X'. Instead, explain X directly with the technical details mentioned.
                    - CAPTURE TRICKS: If the teacher mentions a specific 'trick' (like 'taping a number' in division), you MUST explain the step-by-step logic of that trick.
                    - QUALITY: If the summary is shorter than 200 words, you are failing. Be thorough.
                    Use exactly this JSON schema:
                    {{
                    ""Titol"": ""Descriptive title in {langName}"",
                    ""ResumGeneral"": ""A detailed paragraph summarizing the main concepts, written in {langName}"",
                    ""Subtemes"": [
                        {{ ""Nom"": ""Subtopic name in {langName}"", ""Explicacio"": ""Detailed explanation of this subtopic, written in {langName}"" }}
                    ],
                    ""TasquesProfessor"": [""Task 1 in {langName}"", ""Task 2 in {langName}""],
                    ""PropostaEstudi"": [""Study tip 1 in {langName}"", ""Study tip 2 in {langName}""]
                    }}"
                },
                new {
                    role = "user",
                    content = $"Genera els apunts en {langName} a partir d'aquesta transcripció:\n\n{prompt}"
                }
            },
            max_tokens = 4096,
            temperature = 0.1
        };

        int maxRetries = 3;
        int delaySeconds = 3;

        for (int i = 0; i < maxRetries; i++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await _http.PostAsJsonAsync("https://api.groq.com/openai/v1/chat/completions", payload);
                
                if ((int)response.StatusCode == 429)
                {
                    var backoff = 10 * (i + 1) * (i + 1);
                    if (i == maxRetries - 1)
                    {
                        _logger.LogError("GROQ rate limited (429), failed after {MaxRetries} retries", maxRetries);
                        throw new HttpRequestException("Rate limited after retries");
                    }
                    _logger.LogWarning("GROQ rate limited (429). Waiting {Backoff}s before retry (Attempt {Current}/{Max})", backoff, i + 1, maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(backoff));
                    continue;
                }
                
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                return doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (i == maxRetries - 1)
                {
                    _logger.LogError(ex, "GROQ request failed permanently after {MaxRetries} retries", maxRetries);
                    throw;
                }

                var backoff = delaySeconds * (i + 1);
                _logger.LogWarning("Connection error. Retrying in {Delay}s... (Attempt {Current}/{Max})", backoff, i + 1, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(backoff));
            }
            finally
            {
                response?.Dispose();
            }
        }
        return string.Empty;
    }
}
