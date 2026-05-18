namespace SmartNotes.Api.Models
{
    public class User
    {
        public int Id { get; set; }              // Clau primària
        public string Username { get; set; } = string.Empty;      // Nom d'usuari únic
        public string Email { get; set; } = string.Empty;        // Correu electrònic
        public string PasswordHash { get; set; } = string.Empty; // Contrasenya encriptada
        public int SecondsAvailable { get; set; } = 0; // Minuts disponibles
        public int FailedLoginAttempts { get; set; } = 0; // Comptador d'intents de login fallits
        public DateTime? LockoutEnd { get; set; } // Data i hora de desbloqueig (null si no està bloquejat)
        public string PreferredLanguage { get; set; } = "ca,es"; // Idioma preferit per a respostes de l'IA
        public string Role { get; set; } = "User"; // Rol de l'usuari (ex: User, Admin)
        public string? PasswordResetToken { get; set; } // Token per a restablir contrasenya
        public DateTime? PasswordResetTokenExpires { get; set; } // Expiració
    }
}