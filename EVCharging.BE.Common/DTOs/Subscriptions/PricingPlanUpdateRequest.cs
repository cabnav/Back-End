using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Subscriptions
{
    /// <summary>
    /// Request để cập nhật PricingPlan
    /// </summary>
    public class PricingPlanUpdateRequest
    {
        public string? Name { get; set; }
        public string? PlanType { get; set; }
        public string? Description { get; set; }

        [Range(0, 100000000, ErrorMessage = "Price must be between 0 and 100,000,000")]
        public decimal? Price { get; set; }

        [RegularExpression("^(monthly|yearly)$", ErrorMessage = "BillingCycle must be: monthly or yearly")]
        public string? BillingCycle { get; set; }

        [Range(0, 1, ErrorMessage = "DiscountRate must be between 0 and 1 (0% to 100%)")]
        public decimal? DiscountRate { get; set; }

        public string? TargetAudience { get; set; }
        public string? Benefits { get; set; }
        public bool? IsActive { get; set; }
    }
}

