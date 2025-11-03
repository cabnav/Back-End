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
}
