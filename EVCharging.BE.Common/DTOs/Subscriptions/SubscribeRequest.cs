using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Subscriptions
{
    /// <summary>
    /// Request để đăng ký gói subscription
    /// </summary>
    public class SubscribeRequest
    {
        [Required(ErrorMessage = "Tier is required")]
        public string Tier { get; set; } = string.Empty; // Tên gói (ví dụ: "silver", "gold", "platinum" hoặc tên gói tùy chỉnh)

        [Required(ErrorMessage = "BillingCycle is required")]
        [RegularExpression("^(monthly|yearly)$", ErrorMessage = "BillingCycle must be: monthly or yearly")]
        public string BillingCycle { get; set; } = "monthly";

        [Required(ErrorMessage = "PaymentMethod is required")]
        public string PaymentMethod { get; set; } = "wallet"; // wallet, momo, mock
    }
}

