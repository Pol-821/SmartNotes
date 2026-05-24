using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartNotes.Api.DTOs;
using SmartNotes.Api.Services;
using SmartNotes.Api.Extensions;


namespace SmartNotes.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly JwtService _jwtService;
        private readonly EmailService _emailService;

        public AuthController(UserService userService, JwtService jwtService, EmailService emailService)
        {
            _userService = userService;
            _jwtService = jwtService;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest dto)
        {
            try
            {
                var user = await _userService.RegisterAsync(
                    dto.Username, 
                    dto.Email, 
                    dto.Password,
                    dto.Role,
                    dto.Languages);

                return Ok(new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.Role
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest dto)
        {
            var user = await _userService.GetByLoginIdentifierAsync(dto.Username);
            if (user == null)
                return Unauthorized(new { error = "Credencials incorrectes" });

            if (_userService.IsUserLockedOut(user))
                return Unauthorized(new { error = "Compte bloquejat temporalment per massa intents fallits. Torna-ho a intentar més tard." });

            if (!_userService.VerifyPassword(dto.Password, user.PasswordHash))
            {
                await _userService.RegisterFailedLoginAsync(user);
                return Unauthorized(new { error = "Credencials incorrectes" });
            }
            
            await _userService.RegisterSuccessfulLoginAsync(user);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var accessToken = _jwtService.GenerateToken(user);
            var refreshToken = await _userService.CreateRefreshTokenAsync(user, ip, userAgent);

            return Ok(new 
            {
                accessToken,
                refreshToken = refreshToken.Token,
                role = user.Role,
                email = user.Email
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshRequest dto)
        {
            var storedToken = await _userService.GetActiveRefreshTokenAsync(dto.RefreshToken);
            if (storedToken == null)
                return Unauthorized(new { error = "Refresh token invàlid o caducat" });
            
            var user = storedToken.User;
            storedToken.RevokedAt = DateTime.UtcNow;

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var newAccessToken = _jwtService.GenerateToken(user);
            var newRefreshToken = await _userService.CreateRefreshTokenAsync(user, ip, userAgent);

            return Ok(new 
            {
                token = newAccessToken,
                refreshToken = newRefreshToken.Token
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(LogoutRequest dto)
        {
            var success = await _userService.RevokeRefreshTokenAsync(dto.RefreshToken);
            if (!success)
                return BadRequest(new { error = "No s'ha pogut revocar el refresh token" });

            return Ok(new { message = "Sessió tancada correctament" });
        }

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll()
        {
            var count = await _userService.RevokeAllRefreshTokensForUserAsync(User.GetUserId());
            return Ok(new { message = "Totes les sessions tancades correctament", revokedCount = count });
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var sessions = await _userService.GetActiveSessionsAsync(User.GetUserId());
            return Ok(sessions);
        }
        
        [Authorize]
        [HttpPost("sessions/revoke/{id}")]
        public async Task<IActionResult> RevokeSession(int id)
        {
            var success = await _userService.RevokeSessionAsync(id, User.GetUserId());
            if (!success)
                return NotFound(new { error = "Sessió no trobada" });
            return Ok(new { message = "Sessió revocada correctament" });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest dto)
        {
            var user = await _userService.GetByEmailAsync(dto.Email);
            if (user == null)
            {
                return Ok(new { message = "Si el correu existeix, s'ha enviat un enllaç." });
            }

            user.PasswordResetToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            user.PasswordResetTokenExpires = DateTime.UtcNow.AddMinutes(15); // Caduca en 15 minuts
            await _userService.UpdateUserAsync(user);

            var resetLink = $"http://localhost:5173/reset-password?token={user.PasswordResetToken}";

            var emailBody = $@"
                <h2>Recuperació de Contrasenya - SmartNotes</h2>
                <p>Hola {user.Username},</p>
                <p>Hem rebut una sol·licitud per restablir la teva contrasenya.</p>
                <p>Fes clic a l'enllaç següent per crear-ne una de nova (caduca en 15 minuts):</p>
                <p><a href='{resetLink}' style='padding: 10px 20px; background-color: #2563eb; color: white; text-decoration: none; border-radius: 5px;'>Restablir Contrasenya</a></p>
                <p>Si no has demanat això, pots ignorar aquest correu.</p>";

            await _emailService.SendEmailAsync(user.Email, "Recupera la teva contrasenya", emailBody);

            return Ok(new { message = "Correu enviat correctament." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest dto)
        {
            var user = await _userService.GetByResetTokenAsync(dto.Token);
            if (user == null || user.PasswordResetTokenExpires < DateTime.UtcNow)
                return BadRequest(new { error = "Token invàlid o caducat." });

            user.PasswordHash = _userService.HashPassword(dto.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpires = null;
            user.LockoutEnd = null;
            await _userService.UpdateUserAsync(user);

            return Ok(new { message = "Contrasenya canviada correctament." });
        }
    }
}