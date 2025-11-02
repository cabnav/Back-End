namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// DTO xử lý callback từ MoMo sau khi thanh toán
    /// </summary>
    public class MomoExecuteResponseDto
    {
        public string OrderId { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string OrderInfo { get; set; } = string.Empty;
        public string? PaymentStatus { get; set; }
        public string? ErrorCode { get; set; }
        public string? TransactionId { get; set; }
    }
}

