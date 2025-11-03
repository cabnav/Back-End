using System;

namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// Result DTO cho payment operations (PayByWallet, PayByCash)
    /// </summary>
    public class PaymentResultDto
    {
        public bool Success { get; set; }
        public bool AlreadyPaid { get; set; }
        public string Message { get; set; } = "";
        public PaymentInfoDto? PaymentInfo { get; set; }
        public PaymentInfoDto? ExistingPaymentInfo { get; set; }
        public WalletInfoDto? WalletInfo { get; set; }
        public InvoiceResponseDto? Invoice { get; set; }
    }

    /// <summary>
    /// Thông tin payment
    /// </summary>
    public class PaymentInfoDto
    {
        public int PaymentId { get; set; }
        public int UserId { get; set; }
        public int? SessionId { get; set; }
        public int? ReservationId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string? InvoiceNumber { get; set; }
        public DateTime? PaidAt { get; set; }
        public int? TransactionId { get; set; }
    }

    /// <summary>
    /// Thông tin wallet transaction
    /// </summary>
    public class WalletInfoDto
    {
        public decimal BalanceBefore { get; set; }
        public decimal AmountDeducted { get; set; }
        public decimal BalanceAfter { get; set; }
    }

    /// <summary>
    /// Thông tin invoice
    /// </summary>
    public class InvoiceDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

