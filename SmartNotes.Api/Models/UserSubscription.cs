namespace SmartNotes.Api.Models
{
    public class UserSubscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int PlanId { get; set; }
        public SubscriptionPlan? Plan { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime NextBillingDate { get; set; }
    }
}
