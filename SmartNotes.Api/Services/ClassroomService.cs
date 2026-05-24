using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Data;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Services
{
    public class ClassroomService
    {
        private readonly SmartNotesDbContext _context;

        public ClassroomService(SmartNotesDbContext context)
        {
            _context = context;
        }

        // Llistar totes les aules d'un professor
        public async Task<List<Classroom>> GetClassroomsByUserAsync(int userId)
        {
            return await _context.Classrooms
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        // Crear una nova aula
        public async Task<Classroom> CreateClassroomAsync(int userId, string name, string color)
        {
            var classroom = new Classroom
            {
                UserId = userId,
                Name = name,
                Color = color,
                Code = GenerateClassCode(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Classrooms.Add(classroom);
            await _context.SaveChangesAsync();

            return classroom;
        }

        // Esborrar una aula (Molt important: només el propietari la pot esborrar!)
        public async Task<bool> DeleteClassroomAsync(int id, int userId)
        {
            var classroom = await _context.Classrooms.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (classroom == null) return false;

            _context.Classrooms.Remove(classroom);
            await _context.SaveChangesAsync();
            return true;
        }
        private string GenerateClassCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return string.Create(6, chars, (buffer, alphabet) =>
            {
                var rng = System.Security.Cryptography.RandomNumberGenerator.GetBytes(6);
                for (int i = 0; i < 6; i++)
                    buffer[i] = alphabet[rng[i] % alphabet.Length];
            });
        }

        public async Task<bool> JoinClassroomAsync(int userId, string code)
        {
            // Busquem si existeix alguna classe amb aquest codi (ho passem a majúscules per evitar errors)
            var classroom = await _context.Classrooms.FirstOrDefaultAsync(c => c.Code == code.ToUpper().Trim());
            if (classroom == null) return false;

            // Comprovem que l'alumne no estigui ja matriculat per evitar duplicats
            var alreadyEnrolled = await _context.Enrollments.AnyAsync(e => e.UserId == userId && e.ClassroomId == classroom.Id);
            if (alreadyEnrolled) return true;

            // Creem la matrícula
            var enrollment = new Enrollment { UserId = userId, ClassroomId = classroom.Id };
            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            
            return true;
        }

        // 2. Obtenir les classes on un alumne està matriculat
        public async Task<List<Classroom>> GetEnrolledClassroomsAsync(int userId)
        {
            // Busquem els IDs de les aules d'aquest alumne
            var enrolledClassroomIds = await _context.Enrollments
                .Where(e => e.UserId == userId)
                .Select(e => e.ClassroomId)
                .ToListAsync();

            // Retornem la informació completa d'aquestes aules
            return await _context.Classrooms
                .Where(c => enrolledClassroomIds.Contains(c.Id))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Note>> GetClassroomNotesForStudentAsync(int classroomId, int userId)
        {
            // Comprovem si està matriculat
            var isEnrolled = await _context.Enrollments.AnyAsync(e => e.ClassroomId == classroomId && e.UserId == userId);
            if (!isEnrolled) return new List<Note>(); // Si no està matriculat, retornem una llista buida per denegar l'accés

            // Si està matriculat, li donem els apunts d'aquella aula
            return await _context.Notes
                .Where(n => n.ClassroomId == classroomId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<object?> GetStudentsInClassroomAsync(int classroomId, int professorId)
        {
            // Verifiquem que l'aula és d'aquest professor
            var isOwner = await _context.Classrooms.AnyAsync(c => c.Id == classroomId && c.UserId == professorId);
            if (!isOwner) return null;

            // Busquem els IDs dels alumnes matriculats
            var studentIds = await _context.Enrollments
                .Where(e => e.ClassroomId == classroomId)
                .Select(e => e.UserId)
                .ToListAsync();

            // Retornem només dades segures (sense contrasenyes)
            return await _context.Users
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, u.Email })
                .ToListAsync();
        }

        // 2. Expulsar un alumne de la classe
        public async Task<bool> RemoveStudentAsync(int classroomId, int studentId, int professorId)
        {
            // Verifiquem propietat de l'aula
            var isOwner = await _context.Classrooms.AnyAsync(c => c.Id == classroomId && c.UserId == professorId);
            if (!isOwner) return false;

            // Busquem la matrícula
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.ClassroomId == classroomId && e.UserId == studentId);
            
            if (enrollment == null) return false;

            _context.Enrollments.Remove(enrollment);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}