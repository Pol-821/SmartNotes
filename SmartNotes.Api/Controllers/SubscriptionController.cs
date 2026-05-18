using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Data;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly SmartNotesDbContext _context;

        public SubscriptionController(SmartNotesDbContext context)
        {
            _context = context;
        }

        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
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
                .ToListAsync();

            return Ok(plans);
        }

        [HttpGet("my-subscription")]
        [Authorize]
        public async Task<IActionResult> GetMySubscription()
        {
            var userId = GetUserId();

            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

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
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
        {
            var userId = GetUserId();

            var plan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
            if (plan == null || !plan.IsActive)
                return BadRequest("Pla no vàlid.");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("Usuari no trobat.");

            var existingSub = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.IsActive)
                .FirstOrDefaultAsync();

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

            await _context.SaveChangesAsync();

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
        public async Task<IActionResult> CancelSubscription()
        {
            var userId = GetUserId();

            var subscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.IsActive)
                .FirstOrDefaultAsync();

            if (subscription == null)
                return NotFound("No tens cap subscripció activa.");

            subscription.IsActive = false;
            subscription.EndDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Subscripció cancel·lada correctament." });
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? User.FindFirst("sub");
            return int.Parse(userIdClaim!.Value);
        }
    }

    public record SubscribeRequest(int PlanId);
}
