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
    [Authorize] // Només usuaris loguejats poden accedir-hi
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly SmartNotesDbContext _db;

        public UserController(UserService userService, SmartNotesDbContext db)
        {
            _userService = userService;
            _db = db;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var user = await _userService.GetByIdAsync(User.GetUserId());
            if (user == null) return NotFound();

            int minutesAvailable = user.SecondsAvailable / 60;
            
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == user.Id && s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            int maxSeconds = subscription?.Plan?.SecondsPerMonth ?? 14400;
            string? planName = subscription?.Plan?.Name;
            
            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                SecondsAvailable = user.SecondsAvailable,
                MinutesAvailable = minutesAvailable,
                MaxSeconds = maxSeconds,
                PlanName = planName,
                user.PreferredLanguage
            });
        }

        [HttpPut("settings")]
        [Authorize]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsDto dto)
        {
            var userId = GetUserId();
            var user = await _db.Users.FindAsync(userId);

            if (user == null) return NotFound();

            // 1. Actualitzem els idiomes
            if (!string.IsNullOrEmpty(dto.PreferredLanguage))
            {
                user.PreferredLanguage = dto.PreferredLanguage;
            }

            // 2. Canvi de contrasenya segur
            if (!string.IsNullOrEmpty(dto.NewPassword))
            {
                // Comprovem si ha enviat la contrasenya actual
                if (string.IsNullOrEmpty(dto.CurrentPassword))
                {
                    return BadRequest(new { error = "Has d'introduir la teva contrasenya actual per poder-la canviar." });
                }

                // Verifiquem que la contrasenya actual és correcta
                bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash);
                if (!isPasswordCorrect)
                {
                    return BadRequest(new { error = "La contrasenya actual és incorrecta." });
                }

                // Si tot és correcte, guardem la nova contrasenya
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Configuració actualitzada correctament" });
        }

        public class UpdateSettingsDto
        {
            public string? PreferredLanguage { get; set; }
            public string? CurrentPassword { get; set; } // NOU CAMP
            public string? NewPassword { get; set; }
        }

        private int GetUserId() => User.GetUserId();
    }
}