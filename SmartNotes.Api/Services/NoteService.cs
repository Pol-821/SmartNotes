using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Data;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Services
{
    public class NoteService
    {
        private readonly SmartNotesDbContext _context;
    private readonly ILogger<NoteService> _logger;

        public NoteService(SmartNotesDbContext context, ILogger<NoteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // MODIFICAT: Ara accepta pàgina i mida de pàgina, i retorna el total
        public async Task<(IEnumerable<Note> Notes, int TotalCount)> GetNotesByUserAsync(int userId, int page = 1, int pageSize = 10, CancellationToken ct = default)
        {
            var query = _context.Notes.Where(n => n.UserId == userId);
            
            int total = await query.CountAsync(ct);
            
            var notes = await query
                .OrderByDescending(n => n.CreatedAt) // Més recents primer
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (notes, total);
        }

        public async Task<Note?> GetNoteByIdAsync(int userId, int id, CancellationToken ct = default)
        {
            return await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        }

        public async Task<Note> CreateNoteAsync(int userId, string title, string content, string jobId = "", CancellationToken ct = default)
        {
            var note = new Note
            {
                UserId = userId,
                JobId = jobId,
                Title = title,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Notes.Add(note);
            await _context.SaveChangesAsync(ct);

            return note;
        }

        public async Task<bool> UpdateNoteAsync(int userId, int id, string title, string content, CancellationToken ct = default)
        {
            var note = await GetNoteByIdAsync(userId, id, ct);
            if (note == null) return false;

            note.Title = title;
            note.Content = content;
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeleteNoteAsync(int id, int userId, CancellationToken ct = default)
        {
            var note = await GetNoteByIdAsync(userId, id, ct);
            if (note == null) return false;

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> MoveNoteAsync(int noteId, int userId, int? classroomId, CancellationToken ct = default)
        {
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId, ct);
            
            if (note == null) return false;

            // Li canviem l'identificador de la carpeta
            note.ClassroomId = classroomId;
            
            _context.Notes.Update(note);
            await _context.SaveChangesAsync(ct);
            
            return true;
        }

        public async Task<string?> TogglePublicAccessAsync(int noteId, int userId, bool isPublic, CancellationToken ct = default)
        {
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId, ct);
            if (note == null) return null;

            note.IsPublic = isPublic;

            // Si la fem pública i no té codi, el generem
            if (isPublic && string.IsNullOrEmpty(note.PublicId))
            {
                note.PublicId = Guid.NewGuid().ToString();
            }

            await _context.SaveChangesAsync(ct);
            return note.PublicId;
        }
    }
}