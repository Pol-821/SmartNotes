using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Data;
using SmartNotes.Api.Models;
using System.Security.Cryptography;

namespace SmartNotes.Api.Services
{
    public class UserService
    {
        private readonly SmartNotesDbContext _context;

        public UserService(SmartNotesDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByLoginIdentifierAsync(string identifier)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == identifier || u.Email == identifier);
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }

        public async Task<User> RegisterAsync(string username, string email, string password, string role, List<string> languages)
        {
            // Comprovem si existeix
            if (await _context.Users.AnyAsync(u => u.Username == username))
                throw new Exception("El nom d'usuari ja existeix");
            if (await _context.Users.AnyAsync(u => u.Email == email))
                throw new Exception("El correu electrònic ja existeix");
            
            // Demanem una contrasenya segura
            if (password.Length < 8 || password.Length > 24)
                throw new Exception("La contrasenya ha de tenir entre 8 i 24 caràcters");

            if (!password.Any(char.IsUpper))
                throw new Exception("La contrasenya ha de tenir almenys una lletra majúscula");

            if (!password.Any(char.IsLower))
                throw new Exception("La contrasenya ha de tenir almenys una lletra minúscula");
            if (!password.Any(char.IsDigit))
                throw new Exception("La contrasenya ha de tenir almenys un número");
            
            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                throw new Exception("La contrasenya ha de tenir almenys un caràcter especial");

            if (password.Contains(" "))
                throw new Exception("La contrasenya no pot contenir espais");
            
            if (password.ToLower().Contains(username.ToLower()))
                throw new Exception("La contrasenya no pot contenir el nom d'usuari");

            if (password.ToLower().Contains(email.ToLower()))
                throw new Exception("La contrasenya no pot contenir el correu electrònic");

            if (password.ToLower().Contains("password"))
                throw new Exception("La contrasenya no pot contenir la paraula 'password'");

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            string preferredLanguageString = languages != null && languages.Any()
                ? string.Join(",", languages)
                : "ca,es";

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                SecondsAvailable = 7200*4,
                Role = string.IsNullOrEmpty(role) ? "User" : role,
                PreferredLanguage = preferredLanguageString
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }
        public async Task<RefreshToken> CreateRefreshTokenAsync(User user, string? ip, string? userAgent)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => 
                    rt.UserId == user.Id && 
                    rt.RevokedAt == null && 
                    rt.ExpiresAt > DateTime.UtcNow
                )
                .ToListAsync();
            
            foreach (var t in activeTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
            }

            var randomBytes = RandomNumberGenerator.GetBytes(64);
            var token = Convert.ToBase64String(randomBytes);

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                Device = ParseDeviceFromUserAgent(userAgent ?? string.Empty),
                UserAgent = userAgent,
                IpAddress = ip,
                LastUsed = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt =>
                    rt.Token == token &&
                    rt.RevokedAt == null &&
                    rt.ExpiresAt > DateTime.UtcNow
                );
        }

        public async Task<bool> RevokeRefreshTokenAsync(string token)
        {
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => 
                    rt.Token == token && 
                    rt.RevokedAt == null && 
                    rt.ExpiresAt > DateTime.UtcNow
                );
            if (storedToken == null)
                return false;
            
            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> RevokeAllRefreshTokensForUserAsync(int userId)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => 
                    rt.UserId == userId && 
                    rt.RevokedAt == null && 
                    rt.ExpiresAt > DateTime.UtcNow
                )
                .ToListAsync();
            
            foreach (var t in activeTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return activeTokens.Count;
        }

        public bool IsUserLockedOut(User user)
        {
            return user.LockoutEnd != null && user.LockoutEnd > DateTime.UtcNow;
        }

        public async Task RegisterFailedLoginAsync(User user)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginAttempts = 0; // Reiniciem el comptador després de bloquejar
            }
            await _context.SaveChangesAsync();
        }

        public async Task<User?> GetByResetTokenAsync(string token)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token);
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public async Task RegisterSuccessfulLoginAsync(User user)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _context.SaveChangesAsync();
        }
        private string ParseDeviceFromUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Unknown";

            if (userAgent.Contains("Windows")) return "Windows PC";
            if (userAgent.Contains("Macintosh")) return "Mac";
            if (userAgent.Contains("Android")) return "Android";
            if (userAgent.Contains("iPhone")) return "iPhone";
            if (userAgent.Contains("iPad")) return "iPad";

            return "Other";
        }

        public async Task<bool> RevokeSessionAsync(int tokenId, int userId)
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt =>
                    rt.Id == tokenId &&
                    rt.UserId == userId
                );
            if (token == null)
                return false;

            token.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<object>> GetActiveSessionsAsync(int userId)
        {
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
                .Select(rt => new 
                {
                    rt.Id,
                    rt.Device,
                    rt.UserAgent,
                    rt.IpAddress,
                    rt.CreatedAt,
                    rt.LastUsed,
                    rt.ExpiresAt
                })
                .ToListAsync<object>();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
    }
}