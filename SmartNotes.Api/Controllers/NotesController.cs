using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SmartNotes.Api.Dtos;
using SmartNotes.Api.Services;
using SmartNotes.Api.Models;
using System.IO;
using SmartNotes.Api.Data;
using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Extensions;

namespace SmartNotes.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotesController : ControllerBase
    {
        private readonly NoteService _noteService;
        private readonly TranscriptionQueue _queue;
        private readonly UserService _userService;
        private readonly SmartNotesDbContext _context;
        private readonly R2Service _r2;
        private readonly ILogger<NotesController> _logger;

        public NotesController(NoteService noteService, TranscriptionQueue queue, UserService userService, SmartNotesDbContext context, R2Service r2, ILogger<NotesController> logger)
        {
            _noteService = noteService;
            _queue = queue;
            _userService = userService;
            _context = context;
            _r2 = r2;
            _logger = logger;
        }

    public class MoveNoteDto
    {
        public int? ClassroomId { get; set; }
    }

    public class SetPublicDto
    {
        public bool IsPublic { get; set; }
    }

        private int GetUserId() => User.GetUserId();

        // MODIFICAT: Accepta Query Parameters
        [HttpGet]
        public async Task<IActionResult> GetNotes([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            var userId = GetUserId();
            var result = await _noteService.GetNotesByUserAsync(userId, page, pageSize, ct);
            
            var items = result.Notes.Select(n => new { n.Id, n.Title, n.Content, n.CreatedAt }).ToList();
            return Ok(new 
            {
                TotalItems = result.TotalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize),
                Items = items
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetNote(int id, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var note = await _noteService.GetNoteByIdAsync(userId, id, ct);
            if (note == null) return NotFound();

            return Ok(new { note.Id, note.Title, note.Content, note.CreatedAt });
        }

        [HttpPost]
        public async Task<IActionResult> CreateNote(CreateNoteDto dto, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var user = await _userService.GetByIdAsync(userId, ct);
            if (user == null) return NotFound("Usuari no trobat");

            int costInSeconds = 300;
            if (!await _userService.TryDeductSecondsAsync(userId, costInSeconds, ct))
            {
                return StatusCode(402, new { error = "No tens suficients minuts per processar aquesta transcripció. Si us plau, millora el teu pla." });
            }

            var note = await _noteService.CreateNoteAsync(userId, dto.Title, dto.Content, "", ct);

            return CreatedAtAction(nameof(GetNote), new { id = note.Id }, new { note.Id, note.Title, note.Content, note.CreatedAt });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNote(int id, UpdateNoteDto dto, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var updated = await _noteService.UpdateNoteAsync(userId, id, dto.Title, dto.Content, ct);

            if (!updated) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var deleted = await _noteService.DeleteNoteAsync(id, userId, ct);

            if (!deleted) return NotFound(new { error = "Nota no trobada o no tens permisos per esborrar-la." });
            return Ok(new { message = "Apunt esborrat correctament." });
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndProcessAudio([FromForm] string title, IFormFile audioFile, CancellationToken ct = default)
        {
            var userId = GetUserId(); 
            var user = await _userService.GetByIdAsync(userId, ct);
            if (user == null) return NotFound("Usuari no trobat");

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest("No s'ha enviat cap arxiu d'àudio.");

            // 1. Pujar directament a R2
            var ext = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
            var mimeType = MimeTypeHelper.GetMimeTypeOrDefault(audioFile.FileName);

            string s3Key;
            byte[] audioBytes;
            using (var ms = new MemoryStream())
            {
                await audioFile.CopyToAsync(ms, ct);
                audioBytes = ms.ToArray();
                ms.Position = 0;
                s3Key = await _r2.UploadAsync(ms, audioFile.FileName, mimeType, ct);
            }

            // 2. Extreure la durada real amb ffprobe
            int realDurationSeconds = 0;
            try
            {
                var tempAudioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
                try
                {
                    await System.IO.File.WriteAllBytesAsync(tempAudioPath, audioBytes, ct);
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ffprobe",
                        Arguments = $"-v error -show_entries format=duration -of csv=p=0 \"{tempAudioPath}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = new System.Diagnostics.Process { StartInfo = psi };
                    proc.Start();
                    var output = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync(ct);
                    if (double.TryParse(output.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var dur))
                    {
                        realDurationSeconds = (int)Math.Ceiling(dur);
                    }
                }
                finally
                {
                    if (System.IO.File.Exists(tempAudioPath))
                        System.IO.File.Delete(tempAudioPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error obtenint durada de l'àudio");
                realDurationSeconds = 0;
            }

            if (realDurationSeconds <= 0)
            {
                // Fallback: estimar per mida (assumint ~64kbps per veu)
                realDurationSeconds = (int)(audioFile.Length / 8000.0);
                _logger.LogInformation("Durada estimada per mida: {Duration}s", realDurationSeconds);
            }

            // 3. Comprovar si té prou minuts i cobrar atòmicament
            if (!await _userService.TryDeductSecondsAsync(userId, realDurationSeconds, ct))
            {
                await _r2.DeleteAsync(s3Key, ct);
                int minutsNecessaris = realDurationSeconds / 60;
                return StatusCode(402, new { error = $"L'àudio dura {minutsNecessaris} minuts, però no tens prou saldo." });
            }

            try
            {
                // 5. ENCUAR EL TREBALL A LA IA
                var jobId = Guid.NewGuid();
                var job = new TranscriptionJob
                {
                    Id = jobId.ToString(),
                    UserId = userId,
                    FilePath = s3Key,
                    OriginalFileName = audioFile.FileName,
                    Status = TranscriptionStatus.Pending,
                    Cancellation = new CancellationTokenSource()
                };
                await _queue.EnqueueAsync(job);

                // 6. Crear la nota PENDENT
                string contingutTemporal = $"[⏳ Transcripció en curs...] Aquesta classe ha durat {realDurationSeconds / 60} minuts i {realDurationSeconds % 60} segons. Whisper i Llama hi estan treballant...";
                var note = await _noteService.CreateNoteAsync(userId, string.IsNullOrEmpty(title) ? "Sense títol" : title, contingutTemporal, jobId.ToString(), ct);

                var freshUser = await _userService.GetByIdAsync(userId, ct);
                return Ok(new { 
                    message = "Àudio pujat correctament. La IA ja està treballant.", 
                    audioDuration = realDurationSeconds,
                    remainingBalance = freshUser?.SecondsAvailable ?? 0,
                    noteId = note.Id 
                });
            }
            catch
            {
                // Reemborsar si falla després del cobrament
                await _userService.RefundSecondsAsync(userId, realDurationSeconds, ct);
                await _r2.DeleteAsync(s3Key, ct);
                throw;
            }
        }

        [HttpPut("{id}/move")]
        public async Task<IActionResult> MoveNoteToClassroom(int id, [FromBody] MoveNoteDto dto, CancellationToken ct = default)
        {
            var userId = GetUserId(); // Asumint que tens el mètode GetUserId() aquí
            
            var success = await _noteService.MoveNoteAsync(id, userId, dto.ClassroomId, ct);
            
            if (!success) return NotFound(new { error = "Nota no trobada o no tens permisos." });
            
            return Ok(new { message = "Apunt mogut correctament a l'aula." });
        }

        [HttpPatch("{id}/public")]
        public async Task<IActionResult> SetPublicStatus(int id, [FromBody] SetPublicDto dto, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var publicId = await _noteService.TogglePublicAccessAsync(id, userId, dto.IsPublic, ct);
            return Ok(new { isPublic = dto.IsPublic, publicId });
        }

        [AllowAnonymous]
        [HttpGet("shared/{publicId}")]
        public async Task<IActionResult> GetSharedNote(string publicId, CancellationToken ct = default)
        {
            var note = await _context.Notes
                .Where(n => n.IsPublic && n.PublicId == publicId)
                .Select(n => new { n.Title, n.Content, n.CreatedAt }) // No enviem dades privades
                .FirstOrDefaultAsync(ct);

            if (note == null) return NotFound("Aquests apunts no existeixen o ja no són públics.");

            return Ok(note);
        }

        [HttpGet("{id}/audio")]
        [Authorize]
        public async Task<IActionResult> GetAudioFile(int id, CancellationToken ct = default)
        {
            var currentUserId = GetUserId();

            var note = await _context.Notes.FindAsync(new object[] { id }, ct);
            if (note == null) return NotFound("Apunt no trobat.");

            if (note.UserId != currentUserId)
            {
                return StatusCode(403, new { error = "Aquest àudio no és teu." });
            }

            if (string.IsNullOrEmpty(note.JobId))
                return NotFound("Aquest apunt no té àudio associat.");

            // Buscar el TranscriptionRecord per obtenir la clau R2
            var record = await _context.Transcriptions
                .FirstOrDefaultAsync(t => t.JobId == note.JobId, ct);

            if (record != null && !string.IsNullOrEmpty(record.EnhancedAudioPath))
            {
                try
                {
                    var r2Stream = await _r2.DownloadAsync(record.EnhancedAudioPath, ct);
                    return File(r2Stream, "audio/mpeg", enableRangeProcessing: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error descarregant àudio de R2");
                }
            }

            // Fallback: intentar local (per si hi ha fitxers antics)
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", $"Professor_{note.UserId}", $"audio_{note.JobId}.mp3");
            if (System.IO.File.Exists(filePath))
            {
                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(stream, "audio/mpeg", enableRangeProcessing: true);
            }

            return NotFound("L'arxiu d'àudio no està disponible.");
        }
    }
}