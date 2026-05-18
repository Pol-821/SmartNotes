namespace SmartNotes.Api.Models
{
    public class Enrollment
    {
        public int Id { get; set; }
        public int UserId { get; set; } // L'ID de l'alumne
        public int ClassroomId { get; set; } // L'ID de l'assignatura
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    }
}