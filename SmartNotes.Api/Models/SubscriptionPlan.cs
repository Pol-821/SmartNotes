namespace SmartNotes.Api.Models
{
    public class SubscriptionPlan
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g., "Basic", "Pro", "Enterprise"
        public string Description { get; set; } = string.Empty;
        public decimal PriceMonthly { get; set; } // Price in EUR
        public int SecondsPerMonth { get; set; } // Seconds allocated per month
        public bool IsActive { get; set; } = true;
    }
}
