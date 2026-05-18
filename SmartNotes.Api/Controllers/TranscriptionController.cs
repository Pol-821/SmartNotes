using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Services;
using SmartNotes.Api.Models;
using SmartNotes.Api.Data;
using SmartNotes.Api.Mappers;
using SmartNotes.Api.Extensions;
using System.Security.Claims;

namespace SmartNotes.Api.Controllers;

[ApiController]
[Route("api/transcription")]
public class TranscriptionController : ControllerBase
{
    private readonly WhisperService _whisper;
    private readonly TranscriptionStore _store;
    private readonly TranscriptionQueue _queue;
    private readonly SmartNotesDbContext _db;
    private readonly IConfiguration _config;
    private readonly R2Service _r2;

    public TranscriptionController(WhisperService whisper, TranscriptionStore store, TranscriptionQueue queue, SmartNotesDbContext db, IConfiguration config, R2Service r2)
    {
        _whisper = whisper;
        _store = store;
        _queue = queue;
        _db = db;
        _config = config;
        _r2 = r2;
    }

    public class RetryRequest
    {
        public string LanguageCode { get; set; } = "ca";
    }

    [HttpPost]
    [Authorize]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Transcribe(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var allowedExtensions = new[] { ".wav", ".mp3", ".m4a", ".flac", ".ogg", ".aac", ".wma" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest($"Unsupported file type. Allowed: {string.Join(", ", allowedExtensions)}");

        var mimeType = ext switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            ".aac" => "audio/aac",
            ".wma" => "audio/x-ms-wma",
            _ => "application/octet-stream"
        };

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        var s3Key = await _r2.UploadAsync(ms, file.FileName, mimeType);

        var userID = GetUserId();

        var job = _store.CreateJob();
        job.FilePath = s3Key;
        job.Status = TranscriptionStatus.Pending;
        job.UserId = userID!;
        job.OriginalFileName = file.FileName;

        _store.Update(job);
        _queue.Enqueue(job);

        return Ok(new { jobId = job.Id });
    }

    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        var job = _store.Get(id);
        if (job == null)
            return NotFound();

        return Ok(job.ToDto());
    }

    [HttpPost("{id}/cancel")]
    public IActionResult Cancel(string id)
    {
        var job = _store.Get(id);
        if (job == null)
            return NotFound();

        if (job.Status is TranscriptionStatus.Done or TranscriptionStatus.Error or TranscriptionStatus.Cancelled)
            return BadRequest("Job already finished.");

        job.Cancellation.Cancel();
        job.Status = TranscriptionStatus.Cancelled;
        _store.Update(job);

        return Ok(new { message = "Transcription cancelled." });
    }

    [HttpGet("record/{id}")]
    [Authorize]
    public IActionResult GetRecord(int id)
    {
        var userId = GetUserId();

        var record = _db.Transcriptions
            .FirstOrDefault(t => t.Id == id && t.UserId == userId);

        if (record == null)
            return NotFound();

        return Ok(record);
    }

    [HttpPost("raspberry")]
    public async Task<IActionResult> UploadFromRaspberry(IFormFile file)
    {
        if (!Request.Headers.TryGetValue("X-Serial-Number", out var serialHeader))
            return Unauthorized("Missing serial number");
        
        if (!Request.Headers.TryGetValue("X-Raspberry-Key", out var apiKeyHeader))
            return Unauthorized("Missing secret key");

        var serial = serialHeader.ToString();
        var apiKey = apiKeyHeader.ToString();

        var expectedApiKey = _config["Raspberry:ApiKey"];
        if (string.IsNullOrEmpty(expectedApiKey) || apiKey != expectedApiKey)
            return Unauthorized("Invalid secret key");

        var device = _db.RaspberryDevices.FirstOrDefault(d => d.SerialNumber == serial);

        if (device == null)
            return Unauthorized("Unknown device");
        
        if (device.UserId == null)
            return Unauthorized("Device not linked to any user");

        var userId = device.UserId;

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var allowedExtensions = new[] { ".wav", ".mp3", ".m4a", ".flac", ".ogg", ".aac", ".wma" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest($"Unsupported file type. Allowed: {string.Join(", ", allowedExtensions)}");

        var mimeType = ext switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            ".aac" => "audio/aac",
            ".wma" => "audio/x-ms-wma",
            _ => "application/octet-stream"
        };

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        var s3Key = await _r2.UploadAsync(ms, file.FileName, mimeType);

        var job = _store.CreateJob();
        job.FilePath = s3Key;
        job.Status = TranscriptionStatus.Pending;
        job.UserId = userId.Value;
        job.OriginalFileName = file.FileName;

        _store.Update(job);
        _queue.Enqueue(job);

        return Ok(new { jobId = job.Id });
    }

    [HttpDelete("record/{id}")]
    [Authorize]
    public IActionResult DeleteRecord(int id)
    {
        var userId = GetUserId();

        var record = _db.Transcriptions
            .FirstOrDefault(t => t.Id == id && t.UserId == userId);

        if (record == null)
            return NotFound("Record not found");

        _db.Transcriptions.Remove(record);
        _db.SaveChanges();

        return Ok("Record deleted");
    }

    [HttpGet("history")]
    [Authorize]
    public IActionResult GetHistory(int page = 1, int pageSize = 20)
    {
        var userId = GetUserId();

        var query = _db.Transcriptions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt);

        var total = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new
        {
            total,
            page,
            pageSize,
            items
        });
    }

    [HttpGet("status/{id}")]
    public IActionResult GetStatus(string id)
    {
        var job = _store.Get(id);

        if (job == null)
            return NotFound();

        return Ok(new
        {
            job.Id,
            job.Status,
            job.ProgressMessage,
            job.ProgressPercentage,
            job.AudioDuration,
            job.OriginalFileName,
            job.ErrorMessage
        });
    }
    [HttpPost("{id}/retry")]
    public async Task<IActionResult> RetryTranscription(string id, [FromBody] RetryRequest request)
    {
        var record = await _db.Transcriptions.FirstOrDefaultAsync(t => t.JobId == id);
        if (record == null) return NotFound("Transcripció no trobada.");

        if (string.IsNullOrEmpty(record.EnhancedAudioPath))
        {
            return BadRequest("L'arxiu d'àudio original ja no està disponible per fer el reintent.");
        }

        var retryJob = new TranscriptionJob
        {
            UserId = record.UserId,
            OriginalFileName = record.OriginalFileName + $" (Reintent {request.LanguageCode})",
            FilePath = record.EnhancedAudioPath, 
            IsRetry = true,
            ForcedLanguage = request.LanguageCode.ToLower()
        };

        await _queue.EnqueueAsync(retryJob);

        return Accepted(new { 
            Message = $"Reintent iniciat forçant l'idioma '{request.LanguageCode}'.",
            NewJobId = retryJob.Id
        });
    }

    [HttpGet("active")]
    public IActionResult GetActiveJobs([FromServices] TranscriptionStore store)
    {
        var activeJobs = store.GetAll()
            .Where(j => j.Status != TranscriptionStatus.Done && 
                        j.Status != TranscriptionStatus.Error && 
                        j.Status != TranscriptionStatus.Cancelled)
            .Select(j => new 
            {
                Id = j.Id,
                Status = j.Status.ToString(),
                ProgressMessage = j.ProgressMessage,
                ProgressPercentage = j.ProgressPercentage,
                AudioDuration = j.AudioDuration,
                FileName = j.OriginalFileName
            });

        return Ok(activeJobs);
    }

    [HttpGet("{jobId}/audio")]
    [Authorize]
    public async Task<IActionResult> GetAudioStream(string jobId)
    {
        var userId = GetUserId();

        var record = await _db.Transcriptions.FirstOrDefaultAsync(t => t.JobId == jobId && t.UserId == userId);
        
        if (record == null) 
            return NotFound("Transcripció no trobada o no tens permisos.");

        if (string.IsNullOrEmpty(record.EnhancedAudioPath))
            return NotFound("L'arxiu d'àudio ja no està disponible.");

        var presignedUrl = _r2.GeneratePresignedUrl(record.EnhancedAudioPath, TimeSpan.FromHours(1));
        return Ok(new { url = presignedUrl });
    }

    private int GetUserId() => User.GetUserId();
}