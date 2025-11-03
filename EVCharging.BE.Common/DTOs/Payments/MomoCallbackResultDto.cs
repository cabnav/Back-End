namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// DTO kết quả xử lý callback từ MoMo
    /// </summary>
    public class MomoCallbackResultDto
    {
        public bool Success { get; set; }
        public string? RedirectUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
    }
}

