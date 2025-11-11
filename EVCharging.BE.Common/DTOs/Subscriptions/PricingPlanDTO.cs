namespace EVCharging.BE.Common.DTOs.Subscriptions
{
    /// <summary>
    /// DTO cho PricingPlan
    /// </summary>
    public class PricingPlanDTO
    {
        public int PlanId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PlanType { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? BillingCycle { get; set; }
        public decimal? DiscountRate { get; set; }
        public string? TargetAudience { get; set; }
        public string? Benefits { get; set; }
        public bool? IsActive { get; set; }
    }
}

