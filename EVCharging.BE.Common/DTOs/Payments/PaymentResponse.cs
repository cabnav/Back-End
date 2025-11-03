namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// DTO trả về thông tin payment cho staff
    /// </summary>
    public class PaymentResponse
    {
        public int PaymentId { get; set; }
        public int UserId { get; set; }
        public int? SessionId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}

