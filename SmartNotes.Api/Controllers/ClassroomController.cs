using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Services;
using SmartNotes.Api.Models;
using SmartNotes.Api.Data;
using SmartNotes.Api.Extensions;

namespace SmartNotes.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Seguretat: Només els usuaris amb Token poden passar
    public class ClassroomController : ControllerBase
    {
        private readonly ClassroomService _classroomService;
        private readonly SmartNotesDbContext _context;
        private readonly ILogger<ClassroomController> _logger;

        public ClassroomController(ClassroomService classroomService, SmartNotesDbContext context, ILogger<ClassroomController> logger)
        {
            _classroomService = classroomService;
            _context = context;
            _logger = logger;
        }

        private int GetUserId() => User.GetUserId();

        public class JoinClassroomDto
        {
            public string Code { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyClassrooms(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var classrooms = await _classroomService.GetClassroomsByUserAsync(userId, ct);
            return Ok(classrooms);
        }

        // Creem un DTO ràpid aquí mateix per rebre les dades de forma neta
        public class CreateClassroomDto
        {
            public string Name { get; set; } = string.Empty;
            public string Color { get; set; } = "#3b82f6";
        }

        [HttpPost]
        public async Task<IActionResult> CreateClassroom(CreateClassroomDto dto, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var classroom = await _classroomService.CreateClassroomAsync(userId, dto.Name, dto.Color, ct);
            return Ok(classroom);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClassroom(int id, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var success = await _classroomService.DeleteClassroomAsync(id, userId, ct);
            
            if (!success) return NotFound("Aula no trobada o no tens permisos per esborrar-la.");
            
            return Ok(new { message = "Aula esborrada correctament." });
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinClassroom([FromBody] JoinClassroomDto dto, CancellationToken ct = default)
        {
            var userId = GetUserId(); // ID de l'alumne connectat
            var success = await _classroomService.JoinClassroomAsync(userId, dto.Code, ct);
            
            if (!success) return BadRequest(new { error = "El codi d'aula és invàlid o no existeix." });
            
            return Ok(new { message = "T'has unit a l'aula correctament!" });
        }

        // Endpoint per veure les meves aules com a alumne
        [HttpGet("enrolled")]
        public async Task<IActionResult> GetMyEnrolledClassrooms(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var classrooms = await _classroomService.GetEnrolledClassroomsAsync(userId, ct);
            return Ok(classrooms);
        }

        [HttpGet("note/{noteId}")]
        public async Task<IActionResult> GetSingleNoteForStudent(int noteId, CancellationToken ct = default)
        {
            var userId = GetUserId();
            
            // 1. Busquem l'apunt
            var note = await _context.Notes.FindAsync(new object[] { noteId }, ct);
            if (note == null || note.ClassroomId == null) return NotFound("Apunt no trobat.");

            // 2. Comprovem que l'alumne estigui matriculat a l'aula d'aquest apunt
            var isEnrolled = await _context.Enrollments.AnyAsync(e => e.ClassroomId == note.ClassroomId && e.UserId == userId, ct);
            if (!isEnrolled) return StatusCode(403, new { error = "No tens permís per veure aquest apunt." });

            return Ok(new { note.Id, note.Title, note.Content, note.CreatedAt, note.ClassroomId });
        }

        // Endpoint perquè l'alumne demani els apunts d'una aula concreta
        [HttpGet("{id}/notes")]
        public async Task<IActionResult> GetClassroomNotesForStudent(int id, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var notes = await _classroomService.GetClassroomNotesForStudentAsync(id, userId, ct);
            
            if (notes == null) return StatusCode(403, new { error = "No tens accés a aquesta aula o no existeix." });
            
            return Ok(notes);
        }

        // Endpoint per veure la llista d'alumnes
        [HttpGet("{id}/students")]
        [Authorize]
        public async Task<IActionResult> GetClassroomStudents(int id, CancellationToken ct = default)
        {
            var professorId = GetUserId();
            var students = await _classroomService.GetStudentsInClassroomAsync(id, professorId, ct);
            
            if (students == null) return StatusCode(403, new { error = "No tens permís o l'aula no existeix." });
            
            return Ok(students);
        }

        // Endpoint per expulsar un alumne
        [HttpDelete("{id}/students/{studentId}")]
        [Authorize]
        public async Task<IActionResult> RemoveStudent(int id, int studentId, CancellationToken ct = default)
        {
            var professorId = GetUserId();
            var success = await _classroomService.RemoveStudentAsync(id, studentId, professorId, ct);
            
            if (!success) return BadRequest(new { error = "No s'ha pogut expulsar l'alumne." });
            
            return Ok(new { message = "Alumne expulsat correctament." });
        }
    }
}