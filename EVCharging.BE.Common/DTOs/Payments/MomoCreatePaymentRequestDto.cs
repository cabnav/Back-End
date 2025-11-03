namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// DTO để tạo payment request cho MoMo
    /// </summary>
    public class MomoCreatePaymentRequestDto
    {
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string OrderInfo { get; set; } = string.Empty;
    }
}

