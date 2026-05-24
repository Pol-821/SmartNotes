using Microsoft.Extensions.Hosting;
using SmartNotes.Api.Services;
using SmartNotes.Api.Models;
using SmartNotes.Api.Data;
using SmartNotes.Api.Services.AI;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.EntityFrameworkCore;

public class TranscriptionWorker : BackgroundService
{
    private readonly TranscriptionQueue _queue;
    private readonly TranscriptionStore _store;
    private readonly WhisperService _whisper;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SmartNotesEngine _smartNotesEngine;
    private readonly AudioPreprocessor _audioPreprocessor;
    private readonly R2Service _r2;
    private readonly ILogger<TranscriptionWorker> _logger;

    public TranscriptionWorker(
        TranscriptionQueue queue,
        TranscriptionStore store,
        WhisperService whisper,
        IServiceScopeFactory scopeFactory,
        SmartNotesEngine smartNotesEngine,
        AudioPreprocessor audioPreprocessor,
        R2Service r2,
        ILogger<TranscriptionWorker> logger
    )
    {
        _queue = queue;
        _store = store;
        _whisper = whisper;
        _scopeFactory = scopeFactory;
        _smartNotesEngine = smartNotesEngine;
        _audioPreprocessor = audioPreprocessor;
        _r2 = r2;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);

            if (job == null)
                continue;

            string? localAudioPath = null;
            string? enhancedLocalPath = null;
            string? finalR2Key = null;

            try
            {
                // 1. Baixar l'àudio de R2 a un fitxer temporal
                var ext = Path.GetExtension(job.OriginalFileName) ?? ".wav";
                localAudioPath = Path.Combine(Path.GetTempPath(), $"{job.Id}{ext}");
                await _r2.DownloadToFileAsync(job.FilePath!, localAudioPath, job.Cancellation.Token);

                // 2. Obtenir la durada de l'àudio
                var totalDuration = await _audioPreprocessor.GetAudioDurationSecondsAsync(localAudioPath, job.Cancellation.Token);
                job.AudioDuration = TimeSpan.FromSeconds(totalDuration).ToString(@"hh\:mm\:ss");
                job.Status = TranscriptionStatus.AudioCleaning;
                job.ProgressMessage = "Netejant àudio...";
                job.ProgressPercentage = 5;
                _store.Update(job);

                string finalLanguageToUse;

                // --- EL BYPASS DEL PLA DE RESCAT ---
                if (job.IsRetry && !string.IsNullOrEmpty(job.ForcedLanguage))
                {
                    _logger.LogInformation("Iniciant REINTENT. Saltem la neteja d'àudio i el Jutge.");
                    enhancedLocalPath = localAudioPath;
                    finalLanguageToUse = job.ForcedLanguage;
                    _logger.LogInformation("Forçant l'idioma manualment a: '{Language}'", finalLanguageToUse);
                    job.ProgressPercentage = 30;
                }
                else
                {
                    // --- FLUX NORMAL ---
                    _logger.LogInformation("Iniciant neteja i realçament de veu...");
                    enhancedLocalPath = await _audioPreprocessor.EnhanceAudioWithProgressAsync(localAudioPath, job, job.Cancellation.Token);

                    job.ProgressPercentage = 25;
                    job.ProgressMessage = "Extreient mostra d'idioma...";
                    _store.Update(job);

                    _logger.LogInformation("Extreient fragment de 30 segons per detectar l'idioma...");
                    job.Status = TranscriptionStatus.DetectingLanguage;
                    job.ProgressMessage = "Detectant idioma de la classe...";
                    job.ProgressPercentage = 30;
                    _store.Update(job);

                    string samplePath = await _audioPreprocessor.ExtractLanguageSampleAsync(enhancedLocalPath, job.Cancellation.Token);

                    _logger.LogInformation("L'IA 'Large-v3' està analitzant l'idioma del fragment...");
                    string detectedLang = await _whisper.DetectLanguageAsync(samplePath, job.Cancellation.Token);

                    _logger.LogInformation("L'IA creu que l'idioma és: '{DetectedLang}'", detectedLang);
                    job.ProgressMessage = $"Idioma detectat: {detectedLang}";
                    job.ProgressPercentage = 35;
                    _store.Update(job);

                    string[] idiomesUsuari = { "ca", "es" };

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<SmartNotesDbContext>();
                        var user = await db.Users.FindAsync(job.UserId);
                        
                        if (user != null && !string.IsNullOrWhiteSpace(user.PreferredLanguage))
                        {
                            var raw = user.PreferredLanguage.Split(',', StringSplitOptions.TrimEntries);
                            var valids = new HashSet<string> { "ca", "es", "en", "fr", "de", "it", "pt", "eu", "gl", "zh", "ja", "ko", "ar", "hi", "ru", "nl", "pl", "sv", "da", "fi", "no", "cs", "hu", "ro", "sk", "bg", "el", "hr", "sr", "uk", "tr", "th", "vi", "ms", "id", "tl", "sw", "ta", "te", "ml", "kn", "mr", "gu", "pa", "ne", "bn", "ur", "ku", "ha", "af", "am", "yo", "ig", "om", "so", "mt", "ga", "gd", "cy", "lb", "is", "fo", "ht", "jv", "su", "my", "lo", "km", "bo", "dz", "si", "hy", "ka" };
                            idiomesUsuari = raw.Where(v => valids.Contains(v)).ToArray();
                            if (idiomesUsuari.Length == 0)
                                idiomesUsuari = new[] { "ca", "es" };
                        }
                    }

                    finalLanguageToUse = idiomesUsuari[0];

                    if (!string.IsNullOrEmpty(detectedLang) && idiomesUsuari.Contains(detectedLang))
                    {
                        _logger.LogInformation("L'idioma detectat ('{DetectedLang}') és vàlid per aquest professor. L'aplicarem.", detectedLang);
                        finalLanguageToUse = detectedLang;
                    }
                    else
                    {
                        _logger.LogWarning("L'idioma detectat ('{DetectedLang}') no està a la llista del professor o no s'ha detectat clarament. Forçarem el principal: '{FinalLanguage}'.", detectedLang, finalLanguageToUse);
                    }

                    if (File.Exists(samplePath)) File.Delete(samplePath);
                }

                // --- FASE 1: TRANSCRIPCIÓ ---
                _logger.LogInformation("Iniciant la transcripció del fitxer en idioma '{Language}'...", finalLanguageToUse);
                job.Status = TranscriptionStatus.Transcribing;
                job.ProgressMessage = $"Transcrivint àudio ({job.AudioDuration}) amb Whisper...";
                job.ProgressPercentage = 40;
                _store.Update(job);

                var result = await _whisper.TranscribeWithProgressAsync(enhancedLocalPath, job, finalLanguageToUse, job.Cancellation.Token, totalDuration);
                job.Result = CleanTranscript(result);

                _logger.LogInformation("Transcripció acabada ({Length} caràcters). Iniciant resum amb Groq...", job.Result.Length);
                job.ProgressMessage = "Transcripció completada. Generant resum...";
                job.ProgressPercentage = 70;
                _store.Update(job);

                // --- FASE 2: RESUM ---
                job.Status = TranscriptionStatus.Summarizing;
                job.ProgressMessage = "Generant apunts intel·ligents amb IA...";
                job.ProgressPercentage = 75;
                _store.Update(job);

                var summaryObject = await _smartNotesEngine.SummarizeWithProgressAsync(job.Result, finalLanguageToUse, job, _store, totalDuration, job.Cancellation.Token);
                job.Summary = JsonSerializer.Serialize(summaryObject);
                job.Status = TranscriptionStatus.Done;
                job.ProgressMessage = "Procés completat!";
                job.ProgressPercentage = 100;
                _store.Update(job);

                // --- FASE 3: PUJAR L'ÀUDIO MILLORAT A R2 ---
                if (enhancedLocalPath != null && File.Exists(enhancedLocalPath) && !job.IsRetry)
                {
                    using var fs = new FileStream(enhancedLocalPath, FileMode.Open, FileAccess.Read);
                    finalR2Key = await _r2.UploadAsync(fs, $"audio_{job.Id}.mp3", "audio/mpeg", job.Cancellation.Token);
                    _logger.LogInformation("Àudio millorat pujat a R2 amb key: {Key}", finalR2Key);
                }
                else if (job.IsRetry && localAudioPath != null && File.Exists(localAudioPath))
                {
                    using var fs = new FileStream(localAudioPath, FileMode.Open, FileAccess.Read);
                    finalR2Key = await _r2.UploadAsync(fs, $"audio_{job.Id}.mp3", "audio/mpeg", job.Cancellation.Token);
                    _logger.LogInformation("Àudio millorat pujat a R2 (retry) amb key: {Key}", finalR2Key);
                }

                // --- FASE 4: NETEJA DE FITXERS LOCALS ---
                try 
                {
                    if (!job.IsRetry && localAudioPath != null && File.Exists(localAudioPath))
                        File.Delete(localAudioPath);
                } 
                catch (Exception ex) 
                {
                    _logger.LogWarning(ex, "No s'ha pogut esborrar l'àudio local");
                }

                // --- FASE 5: GUARDAR A LA BASE DE DADES ---
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SmartNotesDbContext>();

                    db.Transcriptions.Add(new TranscriptionRecord
                    {
                        JobId = job.Id.ToString(),
                        UserId = job.UserId ?? 0,
                        OriginalFileName = job.OriginalFileName!,
                        CleanText = job.Result!,
                        CreatedAt = DateTime.UtcNow,
                        EnhancedAudioPath = finalR2Key
                    });

                    if (!string.IsNullOrEmpty(job.Summary))
                    {
                        SmartSummary? summaryData;
                        try
                        {
                            summaryData = JsonSerializer.Deserialize<SmartSummary>(job.Summary!);
                        }
                        catch (JsonException)
                        {
                            summaryData = null;
                        }

                        if (summaryData != null)
                        {
                            await CreateNoteFromSummary(db, summaryData, job);
                        }
                        else
                        {
                            _logger.LogWarning("Resum no disponible. Creant nota amb transcripció pura.");
                            await CreateNoteFromTranscript(db, job);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No hi ha resum. Creant nota amb transcripció pura.");
                        await CreateNoteFromTranscript(db, job);
                    }

                    await db.SaveChangesAsync();

                    // Nota: els segons ja es descompten al controller d'origen
                    // (NotesController.UploadAndProcessAudio). No descomptar aquí
                    // per evitar doble cobrament.
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = TranscriptionStatus.Cancelled;
                job.ProgressMessage = "Transcripció cancel·lada.";
                _store.Update(job);
            }
            catch (Exception ex)
            {
                job.Status = TranscriptionStatus.Error;
                job.ErrorMessage = ex.Message;
                job.ProgressMessage = $"Error: {ex.Message}";
                _store.Update(job);
            }
            finally
            {
                // Neteja de fitxers temporals
                try
                {
                    if (localAudioPath != null && File.Exists(localAudioPath))
                        File.Delete(localAudioPath);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "No s'ha pogut esborrar àudio local"); }
                try
                {
                    if (enhancedLocalPath != null && enhancedLocalPath != localAudioPath && File.Exists(enhancedLocalPath))
                        File.Delete(enhancedLocalPath);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "No s'ha pogut esborrar àudio millorat"); }
                job?.Dispose();
            }
        }
    }

    private string CleanTranscript(string output)
    {
        string finalText;

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        bool isTimestampFormat = lines.Any(l => l.Contains("]"));

        if (isTimestampFormat)
        {
            var cleanedText = new List<string>();
            foreach (var line in lines)
            {
                if (!line.Contains("]")) continue;
                string textOnly = line[(line.IndexOf("]") + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(textOnly)) continue;
                cleanedText.Add(textOnly);
            }
            finalText = string.Join(" ", cleanedText).Trim();
        }
        else
        {
            finalText = string.Join(" ", lines).Trim();
        }

        finalText = Regex.Replace(finalText, @"\b(.+?)(\s+\1)\b", "$1", RegexOptions.IgnoreCase);

        return finalText;
    }

    private async Task CreateNoteFromSummary(SmartNotesDbContext db, SmartSummary summary, TranscriptionJob job)
    {
        string markdownContent = $"{summary.ResumGeneral}\n\n";

        if (summary.Subtemes != null && summary.Subtemes.Any())
        {
            markdownContent += "### Subtemes tractats\n";
            foreach(var subtema in summary.Subtemes)
            {
                markdownContent += $"- **{subtema.Nom}**: {subtema.Explicacio}\n";
            }
            markdownContent += "\n";
        }

        if (summary.TasquesProfessor != null && summary.TasquesProfessor.Any())
        {
            markdownContent += "### Tasques pendents del Professor\n";
            foreach(var tasca in summary.TasquesProfessor)
            {
                markdownContent += $"- [ ] {tasca}\n";
            }
            markdownContent += "\n";
        }

        if (summary.PropostaEstudi != null && summary.PropostaEstudi.Any())
        {
            markdownContent += "### Proposta d'Estudi\n";
            foreach(var consell in summary.PropostaEstudi)
            {
                markdownContent += $"- {consell}\n";
            }
        }

        var notaExistent = await db.Notes.FirstOrDefaultAsync(n => n.JobId == job.Id.ToString());
        if (notaExistent != null)
        {
            notaExistent.Title = string.IsNullOrEmpty(summary.Titol) ? notaExistent.Title : summary.Titol;
            notaExistent.Content = markdownContent;
            db.Notes.Update(notaExistent);
            _logger.LogInformation("Nota existent '{NoteId}' actualitzada.", notaExistent.Id);
        }
        else
        {
            var novaNota = new Note 
            {
                UserId = job.UserId ?? 0,
                JobId = job.Id.ToString(),
                Title = string.IsNullOrEmpty(summary.Titol) ? "Apunts de classe" : summary.Titol,
                Content = markdownContent, 
                CreatedAt = DateTime.UtcNow
            };
            db.Notes.Add(novaNota);
            _logger.LogInformation("S'ha creat una nota nova.");
        }
    }

    private async Task CreateNoteFromTranscript(SmartNotesDbContext db, TranscriptionJob job)
    {
        var notaExistent = await db.Notes.FirstOrDefaultAsync(n => n.JobId == job.Id.ToString());
        if (notaExistent != null)
        {
            notaExistent.Content = job.Result ?? "";
            db.Notes.Update(notaExistent);
        }
        else
        {
            db.Notes.Add(new Note 
            {
                UserId = job.UserId ?? 0,
                JobId = job.Id.ToString(),
                Title = job.OriginalFileName ?? "Apunts de classe",
                Content = job.Result ?? "",
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
