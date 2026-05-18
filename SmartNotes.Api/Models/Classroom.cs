namespace SmartNotes.Api.Models
{
    public class Classroom
    {
        public int Id { get; set; }
        public int UserId { get; set; } // El professor propietari
        public string Name { get; set; } = string.Empty; // Ex: "Matemàtiques 3r ESO"
        public string Color { get; set; } = "#3b82f6"; // Un color per posar-ho bonic al Frontend
        public string Code { get; set; } = string.Empty; // Un codi únic per unir-se a la classe (ex: "ABC123")
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}