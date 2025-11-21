namespace EVCharging.BE.Common.DTOs.Corporates
{
    /// <summary>
    /// DTO response khi thanh toán Corporate Invoice bằng Momo
    /// </summary>
    public class CorporateInvoiceMomoPaymentResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public string PayUrl { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string? QrCodeUrl { get; set; }
        public string? Deeplink { get; set; }
        public int PaymentId { get; set; }
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
    }
}

