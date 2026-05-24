using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Data;
using SmartNotes.Api.Models;
using System.Data;

namespace SmartNotes.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly SmartNotesDbContext _context;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(SmartNotesDbContext context, ILogger<SubscriptionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("plans")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlans(CancellationToken ct = default)
        {
            var plans = await _context.SubscriptionPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.PriceMonthly)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.PriceMonthly,
                    MinutesPerMonth = p.SecondsPerMonth / 60,
                    p.SecondsPerMonth
                })
                .ToListAsync(ct);

            return Ok(plans);
        }

        [HttpGet("my-subscription")]
        [Authorize]
        public async Task<IActionResult> GetMySubscription(CancellationToken ct = default)
        {
            var userId = GetUserId();

            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync(ct);

            if (subscription == null)
                return Ok(new { hasSubscription = false });

            return Ok(new
            {
                hasSubscription = true,
                planName = subscription.Plan!.Name,
                startDate = subscription.StartDate,
                nextBillingDate = subscription.NextBillingDate,
                secondsPerMonth = subscription.Plan.SecondsPerMonth
            });
        }

        [HttpPost("subscribe")]
        [Authorize]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request, CancellationToken ct = default)
        {
            var userId = GetUserId();

            var plan = await _context.SubscriptionPlans.FindAsync(new object[] { request.PlanId }, ct);
            if (plan == null || !plan.IsActive)
                return BadRequest("Pla no vàlid.");

            if (plan.PriceMonthly > 0 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")))
            {
                return StatusCode(402, new { error = "Aquest pla requereix pagament. Configura STRIPE_SECRET_KEY al servidor." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var user = await _context.Users.FindAsync(new object[] { userId }, ct);
            if (user == null)
                return NotFound("Usuari no trobat.");

            var existingSub = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.IsActive)
                .FirstOrDefaultAsync(ct);

            if (existingSub != null && existingSub.PlanId == plan.Id)
            {
                return BadRequest(new { error = "Ja estàs subscrit a aquest pla." });
            }

            if (existingSub != null)
            {
                existingSub.IsActive = false;
                existingSub.EndDate = DateTime.UtcNow;
            }

            var newSubscription = new UserSubscription
            {
                UserId = userId,
                PlanId = plan.Id,
                StartDate = DateTime.UtcNow,
                NextBillingDate = DateTime.UtcNow.AddMonths(1),
                IsActive = true
            };

            _context.UserSubscriptions.Add(newSubscription);
            user.SecondsAvailable += plan.SecondsPerMonth;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Ok(new
            {
                message = $"T'has subscrit al pla {plan.Name}!",
                secondsAdded = plan.SecondsPerMonth,
                newBalance = user.SecondsAvailable,
                nextBillingDate = newSubscription.NextBillingDate
            });
        }

        [HttpPost("cancel")]
        [Authorize]
        public async Task<IActionResult> CancelSubscription(CancellationToken ct = default)
        {
            var userId = GetUserId();

            var subscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.IsActive)
                .FirstOrDefaultAsync(ct);

            if (subscription == null)
                return NotFound("No tens cap subscripció activa.");

            subscription.IsActive = false;
            subscription.EndDate = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            return Ok(new { message = "Subscripció cancel·lada correctament." });
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? User.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("Token invàlid o usuari no identificat.");
            return userId;
        }
    }

    public record SubscribeRequest(int PlanId);
}
