using System;
using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// Request để tạo payment mới
    /// </summary>
    public class PaymentCreateRequest
    {
        [Required]
        public int UserId { get; set; }
        
        public int? SessionId { get; set; }
        
        public int? ReservationId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        [Required]
        public string PaymentMethod { get; set; } = ""; // "wallet", "vnpay", "momo", "credit_card", "corporate_billing"
        
        public string? Description { get; set; }
        
        public string? ReturnUrl { get; set; } // URL để redirect sau khi thanh toán
    }
}
