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

        public NotesController(NoteService noteService, TranscriptionQueue queue, UserService userService, SmartNotesDbContext context, R2Service r2)
        {
            _noteService = noteService;
            _queue = queue;
            _userService = userService;
            _context = context;
            _r2 = r2;
        }

        public class MoveNoteDto
        {
            public int? ClassroomId { get; set; }
        }

        private int GetUserId() => User.GetUserId();

        // MODIFICAT: Accepta Query Parameters
        [HttpGet]
        public async Task<IActionResult> GetNotes([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetUserId();
            var result = await _noteService.GetNotesByUserAsync(userId, page, pageSize);
            
            // Retornem un objecte de paginació estàndard de la indústria
            return Ok(new 
            {
                TotalItems = result.TotalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize),
                Items = result.Notes
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetNote(int id)
        {
            var userId = GetUserId();
            var note = await _noteService.GetNoteByIdAsync(userId, id);
            if (note == null) return NotFound();

            return Ok(note);
        }

        [HttpPost]
        public async Task<IActionResult> CreateNote(CreateNoteDto dto)
        {
            var userId = GetUserId();
            var user = await _userService.GetByIdAsync(userId);
            if (user == null) return NotFound("Usuari no trobat");

            int costInSeconds = 300;
            if (user.SecondsAvailable < costInSeconds)
            {
                return StatusCode(402, new { error = "No tens suficients minuts per processar aquesta transcripció. Si us plau, millora el teu pla." });
            }

            var note = await _noteService.CreateNoteAsync(userId, dto.Title, dto.Content, "");

            user.SecondsAvailable -= costInSeconds;
            await _userService.UpdateUserAsync(user);

            return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNote(int id, UpdateNoteDto dto)
        {
            var userId = GetUserId();
            var updated = await _noteService.UpdateNoteAsync(userId, id, dto.Title, dto.Content);

            if (!updated) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            var userId = GetUserId();
            var deleted = await _noteService.DeleteNoteAsync(id, userId);

            if (!deleted) return NotFound(new { error = "Nota no trobada o no tens permisos per esborrar-la." });
            return Ok(new { message = "Apunt esborrat correctament." });
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndProcessAudio([FromForm] string title, IFormFile audioFile)
        {
            var userId = GetUserId(); 
            var user = await _userService.GetByIdAsync(userId);
            if (user == null) return NotFound("Usuari no trobat");

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest("No s'ha enviat cap arxiu d'àudio.");

            // 1. Pujar directament a R2
            var ext = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
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

            string s3Key;
            using (var ms = new MemoryStream())
            {
                await audioFile.CopyToAsync(ms);
                ms.Position = 0;
                s3Key = await _r2.UploadAsync(ms, audioFile.FileName, mimeType);
            }

            // 2. Extreure la durada real amb ffprobe
            int realDurationSeconds = 0;
            var tempAudioPath = Path.GetTempFileName() + ext;
            try
            {
                using (var fs = new FileStream(tempAudioPath, FileMode.Create))
                {
                    await audioFile.CopyToAsync(fs);
                }
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v error -show_entries format=duration -of csv=p=0 \"{tempAudioPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = new System.Diagnostics.Process { StartInfo = psi };
                proc.Start();
                var output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                if (double.TryParse(output.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var dur))
                {
                    realDurationSeconds = (int)Math.Ceiling(dur);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NOTES] Error obtenint durada: {ex.Message}");
                realDurationSeconds = 0;
            }
            finally
            {
                if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);
            }

            if (realDurationSeconds <= 0)
            {
                // Fallback: estimar per mida (assumint ~64kbps per veu)
                var fileSizeBytes = audioFile.Length;
                realDurationSeconds = (int)(fileSizeBytes / 8000.0);
                Console.WriteLine($"[NOTES] Durada estimada per mida: {realDurationSeconds}s");
            }

            // 3. Comprovar si té prou minuts
            if (user.SecondsAvailable < realDurationSeconds)
            {
                await _r2.DeleteAsync(s3Key);
                int minutsNecessaris = realDurationSeconds / 60;
                int minutsDisponibles = user.SecondsAvailable / 60;
                return StatusCode(402, new { error = $"L'àudio dura {minutsNecessaris} minuts, però només et queden {minutsDisponibles} minuts." });
            }

            // 4. Cobrar el peatge
            user.SecondsAvailable -= realDurationSeconds;
            await _userService.UpdateUserAsync(user);

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
            var note = await _noteService.CreateNoteAsync(userId, string.IsNullOrEmpty(title) ? "Sense títol" : title, contingutTemporal, jobId.ToString());

            return Ok(new { 
                message = "Àudio pujat correctament. La IA ja està treballant.", 
                audioDuration = realDurationSeconds,
                remainingBalance = user.SecondsAvailable,
                noteId = note.Id 
            });
        }

        [HttpPut("{id}/move")]
        public async Task<IActionResult> MoveNoteToClassroom(int id, [FromBody] MoveNoteDto dto)
        {
            var userId = GetUserId(); // Asumint que tens el mètode GetUserId() aquí
            
            var success = await _noteService.MoveNoteAsync(id, userId, dto.ClassroomId);
            
            if (!success) return NotFound(new { error = "Nota no trobada o no tens permisos." });
            
            return Ok(new { message = "Apunt mogut correctament a l'aula." });
        }

        [HttpPatch("{id}/public")]
        public async Task<IActionResult> SetPublicStatus(int id, [FromBody] bool isPublic)
        {
            var userId = GetUserId();
            var publicId = await _noteService.TogglePublicAccessAsync(id, userId, isPublic);
            return Ok(new { isPublic, publicId });
        }

        [AllowAnonymous]
        [HttpGet("shared/{publicId}")]
        public async Task<IActionResult> GetSharedNote(string publicId)
        {
            var note = await _context.Notes
                .Where(n => n.IsPublic && n.PublicId == publicId)
                .Select(n => new { n.Title, n.Content, n.CreatedAt }) // No enviem dades privades
                .FirstOrDefaultAsync();

            if (note == null) return NotFound("Aquests apunts no existeixen o ja no són públics.");

            return Ok(note);
        }

        [HttpGet("{id}/audio")]
        [Authorize]
        public async Task<IActionResult> GetAudioFile(int id)
        {
            var currentUserId = GetUserId(); // El que demana l'àudio (profe o alumne)

            // 1. Busquem l'apunt a la base de dades
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound("Apunt no trobat.");

            // 2. Mesures de seguretat
            // Si ets professor, ha de ser teu.
            // Si ets alumne, hauries de passar per la comprovació de matrícules (ho ometo per brevetat, però pots afegir-hi l'if que vam fer abans).
            if (User.FindFirst(ClaimTypes.Role)?.Value != "Alumne" && note.UserId != currentUserId) 
            {
                return Forbid("Aquest àudio no és teu.");
            }

            // 3. Construïm la ruta intel·ligent a la carpeta del professor
            // Com que l'arxiu FFMPEG surt en .wav, el busquem així.
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", $"Professor_{note.UserId}", $"audio_{note.JobId}.mp3");

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("L'arxiu d'àudio físic no es troba al servidor."); 
            }

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            // Retornem el fitxer permetent "avançar i retrocedir" (Range Processing)
            return File(stream, "audio/wav", enableRangeProcessing: true);
        }
    }
}