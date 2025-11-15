namespace EVCharging.BE.Common.DTOs.Subscriptions
{
    /// <summary>
    /// Response về subscription của user
    /// </summary>
    public class SubscriptionResponse
    {
        public string? Tier { get; set; }
        public decimal DiscountRate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public string? BillingCycle { get; set; }
        public decimal? Price { get; set; }
        public string? PaymentUrl { get; set; } // URL thanh toán MoMo (nếu payment method là momo)
    }
}

