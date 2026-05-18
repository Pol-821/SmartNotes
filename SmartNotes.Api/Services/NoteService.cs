using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Data;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Services
{
    public class NoteService
    {
        private readonly SmartNotesDbContext _context;

        public NoteService(SmartNotesDbContext context)
        {
            _context = context;
        }

        // MODIFICAT: Ara accepta pàgina i mida de pàgina, i retorna el total
        public async Task<(IEnumerable<Note> Notes, int TotalCount)> GetNotesByUserAsync(int userId, int page = 1, int pageSize = 10)
        {
            var query = _context.Notes.Where(n => n.UserId == userId);
            
            int total = await query.CountAsync();
            
            var notes = await query
                .OrderByDescending(n => n.CreatedAt) // Més recents primer
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (notes, total);
        }

        public async Task<Note?> GetNoteByIdAsync(int userId, int id)
        {
            return await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        }

        public async Task<Note> CreateNoteAsync(int userId, string title, string content, string jobId = "")
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
            await _context.SaveChangesAsync();

            return note;
        }

        public async Task<bool> UpdateNoteAsync(int userId, int id, string title, string content)
        {
            var note = await GetNoteByIdAsync(userId, id);
            if (note == null) return false;

            note.Title = title;
            note.Content = content;
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteNoteAsync(int id, int userId)
        {
            var note = await GetNoteByIdAsync(userId, id);
            if (note == null) return false;

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MoveNoteAsync(int noteId, int userId, int? classroomId)
        {
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
            
            if (note == null) return false;

            // Li canviem l'identificador de la carpeta
            note.ClassroomId = classroomId;
            
            _context.Notes.Update(note);
            await _context.SaveChangesAsync();
            
            return true;
        }

        public async Task<string?> TogglePublicAccessAsync(int noteId, int userId, bool isPublic)
        {
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
            if (note == null) return null;

            note.IsPublic = isPublic;

            // Si la fem pública i no té codi, el generem
            if (isPublic && string.IsNullOrEmpty(note.PublicId))
            {
                note.PublicId = Guid.NewGuid().ToString();
            }

            await _context.SaveChangesAsync();
            return note.PublicId;
        }
    }
}