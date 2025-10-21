using System;
using System.Collections.Generic;

namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// Response cho payment operations
    /// </summary>
    public class PaymentResponse
    {
        public int PaymentId { get; set; }
        public int UserId { get; set; }
        public int? SessionId { get; set; }
        public int? ReservationId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string? InvoiceNumber { get; set; }
        public string? TransactionId { get; set; } // External payment gateway transaction ID
        public string? PaymentUrl { get; set; } // URL để redirect user đến payment gateway
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response cho payment gateway callback
    /// </summary>
    public class PaymentCallbackResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int PaymentId { get; set; }
        public string? TransactionId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; } = "";
    }

    /// <summary>
    /// Request cho payment gateway callback
    /// </summary>
    public class PaymentCallbackRequest
    {
        public string TransactionId { get; set; } = "";
        public string PaymentId { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Signature { get; set; }
        public string? Message { get; set; }
    }
}
