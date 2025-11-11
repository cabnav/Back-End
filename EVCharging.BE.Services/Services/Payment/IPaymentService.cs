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

        /// <summary>
        /// Lấy danh sách sessions chưa thanh toán của user (để user check và bấm thanh toán)
        /// </summary>
        Task<UnpaidSessionsResponse> GetUnpaidSessionsAsync(int userId, int skip = 0, int take = 20);

        /// <summary>
        /// Lấy danh sách invoices đã thanh toán của user
        /// </summary>
        Task<PaidInvoicesResponse> GetPaidInvoicesAsync(int userId, int skip = 0, int take = 20);

    }
}
