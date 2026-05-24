using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserService userService, JwtService jwtService, EmailService emailService, IConfiguration config, ILogger<AuthController> logger)
        {
            _userService = userService;
            _jwtService = jwtService;
            _emailService = emailService;
            _config = config;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest dto, CancellationToken ct = default)
        {
            try
            {
                var user = await _userService.RegisterAsync(
                    dto.Username, 
                    dto.Email, 
                    dto.Password,
                    dto.Role,
                    dto.Languages,
                    ct);

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
        public async Task<IActionResult> Login(LoginRequest dto, CancellationToken ct = default)
        {
            var user = await _userService.GetByLoginIdentifierAsync(dto.Identifier, ct);
            if (user == null)
                return Unauthorized(new { error = "Credencials incorrectes" });

            if (_userService.IsUserLockedOut(user))
                return Unauthorized(new { error = "Compte bloquejat temporalment per massa intents fallits. Torna-ho a intentar més tard." });

            if (!_userService.VerifyPassword(dto.Password, user.PasswordHash))
            {
                await _userService.RegisterFailedLoginAsync(user, ct);
                return Unauthorized(new { error = "Credencials incorrectes" });
            }
            
            await _userService.RegisterSuccessfulLoginAsync(user, ct);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var accessToken = _jwtService.GenerateToken(user);
            var refreshToken = await _userService.CreateRefreshTokenAsync(user, ip, userAgent, ct);

            return Ok(new 
            {
                accessToken,
                refreshToken = refreshToken.Token,
                role = user.Role,
                email = user.Email
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshRequest dto, CancellationToken ct = default)
        {
            var storedToken = await _userService.GetActiveRefreshTokenAsync(dto.RefreshToken, ct);
            if (storedToken == null)
                return Unauthorized(new { error = "Refresh token invàlid o caducat" });
            
            var user = storedToken.User;
            storedToken.RevokedAt = DateTime.UtcNow;

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var newAccessToken = _jwtService.GenerateToken(user);
            var newRefreshToken = await _userService.CreateRefreshTokenAsync(user, ip, userAgent, ct);

            return Ok(new 
            {
                token = newAccessToken,
                refreshToken = newRefreshToken.Token
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(LogoutRequest dto, CancellationToken ct = default)
        {
            var success = await _userService.RevokeRefreshTokenAsync(dto.RefreshToken, ct);
            if (!success)
                return BadRequest(new { error = "No s'ha pogut revocar el refresh token" });

            return Ok(new { message = "Sessió tancada correctament" });
        }

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll(CancellationToken ct = default)
        {
            var count = await _userService.RevokeAllRefreshTokensForUserAsync(User.GetUserId(), ct);
            return Ok(new { message = "Totes les sessions tancades correctament", revokedCount = count });
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions(CancellationToken ct = default)
        {
            var sessions = await _userService.GetActiveSessionsAsync(User.GetUserId(), ct);
            return Ok(sessions);
        }
        
        [Authorize]
        [HttpPost("sessions/revoke/{id}")]
        public async Task<IActionResult> RevokeSession(int id, CancellationToken ct = default)
        {
            var success = await _userService.RevokeSessionAsync(id, User.GetUserId(), ct);
            if (!success)
                return NotFound(new { error = "Sessió no trobada" });
            return Ok(new { message = "Sessió revocada correctament" });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest dto, CancellationToken ct = default)
        {
            var user = await _userService.GetByEmailAsync(dto.Email, ct);
            if (user == null)
            {
                return Ok(new { message = "Si el correu existeix, s'ha enviat un enllaç." });
            }

            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.PasswordResetToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
            user.PasswordResetTokenExpires = DateTime.UtcNow.AddMinutes(15);

            // Enviem el token en brut per email (NO l'hash)
            var baseUrl = _config["Frontend:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            var resetLink = $"{baseUrl}/reset-password?token={rawToken}";
            await _userService.UpdateUserAsync(user, ct);



            var emailBody = $@"
                <h2>Recuperació de Contrasenya - SmartNotes</h2>
                <p>Hola {user.Username},</p>
                <p>Hem rebut una sol·licitud per restablir la teva contrasenya.</p>
                <p>Fes clic a l'enllaç següent per crear-ne una de nova (caduca en 15 minuts):</p>
                <p><a href='{resetLink}' style='padding: 10px 20px; background-color: #2563eb; color: white; text-decoration: none; border-radius: 5px;'>Restablir Contrasenya</a></p>
                <p>Si no has demanat això, pots ignorar aquest correu.</p>";

            await _emailService.SendEmailAsync(user.Email, "Recupera la teva contrasenya", emailBody, ct);

            return Ok(new { message = "Correu enviat correctament." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest dto, CancellationToken ct = default)
        {
            var user = await _userService.GetByResetTokenAsync(dto.Token, ct);
            if (user == null || user.PasswordResetTokenExpires == null || user.PasswordResetTokenExpires.Value < DateTime.UtcNow)
                return BadRequest(new { error = "Token invàlid o caducat." });

            if (dto.NewPassword.Length < 8 || dto.NewPassword.Length > 24 || !dto.NewPassword.Any(char.IsUpper) || !dto.NewPassword.Any(char.IsLower) || !dto.NewPassword.Any(char.IsDigit) || !dto.NewPassword.Any(ch => !char.IsLetterOrDigit(ch)))
                return BadRequest(new { error = "La contrasenya ha de tenir entre 8 i 24 caràcters, majúscula, minúscula, número i caràcter especial." });

            user.PasswordHash = _userService.HashPassword(dto.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpires = null;
            user.LockoutEnd = null;
            await _userService.UpdateUserAsync(user, ct);

            return Ok(new { message = "Contrasenya canviada correctament." });
        }
    }
}