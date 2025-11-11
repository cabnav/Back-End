using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Subscriptions
{
    /// <summary>
    /// Request để tạo PricingPlan mới
    /// </summary>
    public class PricingPlanCreateRequest
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;

        public string? PlanType { get; set; }
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0, 100000000, ErrorMessage = "Price must be between 0 and 100,000,000")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "BillingCycle is required")]
        [RegularExpression("^(monthly|yearly)$", ErrorMessage = "BillingCycle must be: monthly or yearly")]
        public string BillingCycle { get; set; } = "monthly";

        [Required(ErrorMessage = "DiscountRate is required")]
        [Range(0, 1, ErrorMessage = "DiscountRate must be between 0 and 1 (0% to 100%)")]
        public decimal DiscountRate { get; set; }

        public string? TargetAudience { get; set; }
        public string? Benefits { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

