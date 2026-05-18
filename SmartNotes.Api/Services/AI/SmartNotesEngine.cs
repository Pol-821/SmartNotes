using System.Text.Json;
using SmartNotes.Api.Models;
using Microsoft.Extensions.Logging;

namespace SmartNotes.Api.Services.AI;
public class SmartNotesEngine
{
    private readonly GroqClient _groq;
    private readonly ILogger<SmartNotesEngine> _logger;

    public SmartNotesEngine(GroqClient groq, ILogger<SmartNotesEngine> logger)
    {
        _groq = groq;
        _logger = logger;
    }

    public async Task<SmartSummary> SummarizeAsync(string text, string language = "es")
    {
        return await SummarizeWithProgressAsync(text, language, null, null, 0);
    }

    public async Task<SmartSummary> SummarizeWithProgressAsync(string text, string language, TranscriptionJob? job, TranscriptionStore? store, double totalDuration)
    {
        var chunks = SplitText(text, 6000);
        var finalSummary = new SmartSummary();

        _logger.LogInformation("Text original dividit en {Count} chunks (idioma: {Language})", chunks.Count, language);

        var totalDurationFormatted = TimeSpan.FromSeconds(totalDuration).ToString(@"hh\:mm\:ss");

        for (int i = 0; i < chunks.Count; i++)
        {
            if (i > 0) await Task.Delay(TimeSpan.FromSeconds(8));

            int percentage = (i * 100) / chunks.Count;
            string barra = new string('█', percentage / 10) + new string('░', 10 - (percentage / 10));
            
            if (job != null)
            {
                var summarizingProgress = 70 + (int)((double)i / chunks.Count * 25);
                job.ProgressMessage = $"Generant apunts... {barra} {percentage}% (fragment {i + 1}/{chunks.Count})";
                job.ProgressPercentage = Math.Max(job.ProgressPercentage, summarizingProgress);
                store?.Update(job);
            }
            
            _logger.LogInformation("Processing chunk {Current}/{Total} ({Percentage}%)", i + 1, chunks.Count, percentage);

            try
            {
                var chunkSummary = await ProcessSingleChunkAsync(chunks[i], language);
                
                if (i == 0)
                {
                    finalSummary.Titol = chunkSummary.Titol;
                    finalSummary.ResumGeneral = chunkSummary.ResumGeneral;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(chunkSummary.ResumGeneral))
                    {
                        finalSummary.ResumGeneral += "\n\n" + chunkSummary.ResumGeneral;
                    }
                }
                finalSummary.Subtemes.AddRange(chunkSummary.Subtemes);
                finalSummary.TasquesProfessor.AddRange(chunkSummary.TasquesProfessor);
                finalSummary.PropostaEstudi.AddRange(chunkSummary.PropostaEstudi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk {ChunkNum} failed", i + 1);
            }
        }

        _logger.LogInformation("Summary generation completed with {Subtopics} subtopics, {Tasks} tasks, {StudyTips} study tips",
            finalSummary.Subtemes.Count, finalSummary.TasquesProfessor.Count, finalSummary.PropostaEstudi.Count);

        finalSummary.TasquesProfessor = finalSummary.TasquesProfessor.Distinct().ToList();
        finalSummary.PropostaEstudi = finalSummary.PropostaEstudi.Distinct().ToList();

        return finalSummary;
    }

    private async Task<SmartSummary> ProcessSingleChunkAsync(string chunkText, string language)
    {
        var rawResponse = await _groq.ChatAsync(chunkText, language);

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            throw new Exception("Groq ha retornat una resposta buida o ha fet un Timeout.");
        }

        string cleanJson = ExtractJson(rawResponse);

        if (string.IsNullOrEmpty(cleanJson))
        {
            _logger.LogWarning($"Resposta del LLM no conté JSON vàlid. Resposta RAW: {rawResponse.Substring(0, Math.Min(500, rawResponse.Length))}");
            return new SmartSummary();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var summary = JsonSerializer.Deserialize<SmartSummary>(cleanJson, options) ?? new SmartSummary();

            if (string.IsNullOrEmpty(summary.Titol) && summary.ExtraData != null)
            {
                var possibleKeys = new[] { "Title", "title", "titol", "TÍTOL", "Titulo", "titulo" };
                foreach (var key in possibleKeys)
                {
                    if (summary.ExtraData.TryGetValue(key, out var value))
                    {
                        summary.Titol = value.GetString() ?? string.Empty;
                        break; 
                    }
                }
            }

            return summary;
        }
        catch (JsonException)
        {
            _logger.LogWarning($"JSON del LLM invàlid. Contingut: {cleanJson.Substring(0, Math.Min(200, cleanJson.Length))}");
            return new SmartSummary();
        }
    }

    private string ExtractJson(string rawResponse)
    {
        string text = rawResponse.Trim();

        if (text.StartsWith("```"))
        {
            int firstNewline = text.IndexOf('\n');
            if (firstNewline != -1)
            {
                text = text.Substring(firstNewline + 1).Trim();
                if (text.StartsWith("json"))
                {
                    text = text.Substring(4).Trim();
                }
            }
            int lastBackticks = text.LastIndexOf("```");
            if (lastBackticks != -1)
            {
                text = text.Substring(0, lastBackticks).Trim();
            }
        }

        int startIndex = text.IndexOf('{');
        int endIndex = text.LastIndexOf('}');

        if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
        {
            return "";
        }

        return text.Substring(startIndex, endIndex - startIndex + 1);
    }

    private List<string> SplitText(string text, int maxChars)
    {
        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = "";

        foreach (var word in words)
        {
            if (currentChunk.Length + word.Length > maxChars)
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = "";
            }
            currentChunk += word + " ";
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add(currentChunk.Trim());
        }

        return chunks;
    }
}
