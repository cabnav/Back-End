using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.Services.Services.Payment
{
    /// <summary>
    /// Service quản lý thanh toán cho phiên sạc
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Thanh toán phiên sạc bằng ví - Trừ tiền từ ví và tạo hóa đơn
        /// </summary>
        Task<PaymentResultDto> PayByWalletAsync(int sessionId, int userId);

        /// <summary>
        /// Thanh toán phiên sạc bằng tiền mặt - Tạo hóa đơn thanh toán thành công
        /// </summary>
        Task<PaymentResultDto> PayByCashAsync(int sessionId, int userId);
    }

    /// <summary>
    /// DTO kết quả thanh toán
    /// </summary>
    public class PaymentResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PaymentInfoDto? PaymentInfo { get; set; }
        public WalletInfoDto? WalletInfo { get; set; }
        public InvoiceResponseDto? Invoice { get; set; }
        public bool AlreadyPaid { get; set; }
        public PaymentInfoDto? ExistingPaymentInfo { get; set; }
    }

    public class PaymentInfoDto
    {
        public int PaymentId { get; set; }
            public int? SessionId { get; set; }
        public int? ReservationId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }
        public DateTime? PaidAt { get; set; }
        public int? TransactionId { get; set; }
    }

    public class WalletInfoDto
    {
        public decimal BalanceBefore { get; set; }
        public decimal AmountDeducted { get; set; }
        public decimal BalanceAfter { get; set; }
    }
}

