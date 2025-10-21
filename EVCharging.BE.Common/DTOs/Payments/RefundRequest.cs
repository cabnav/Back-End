using System;
using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// Request để hoàn tiền
    /// </summary>
    public class RefundRequest
    {
        [Required]
        public int PaymentId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Refund amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        [Required]
        public string Reason { get; set; } = "";
        
        public string? AdminNote { get; set; }
    }

    /// <summary>
    /// Response cho refund operation
    /// </summary>
    public class RefundResponse
    {
        public int RefundId { get; set; }
        public int PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
        public string Reason { get; set; } = "";
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
