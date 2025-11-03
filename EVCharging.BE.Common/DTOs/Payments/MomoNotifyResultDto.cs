namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// DTO kết quả xử lý notify từ MoMo
    /// </summary>
    public class MomoNotifyResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
    }
}

