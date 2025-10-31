using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.Services.Services.Payment
{
    /// <summary>
    /// Service quản lý hóa đơn
    /// </summary>
    public interface IInvoiceService
    {
        /// <summary>
        /// Lấy hóa đơn theo InvoiceId
        /// </summary>
        Task<InvoiceResponseDto?> GetInvoiceByIdAsync(int invoiceId, int currentUserId);

        /// <summary>
        /// Lấy hóa đơn theo SessionId
        /// </summary>
        Task<InvoiceResponseDto?> GetInvoiceBySessionIdAsync(int sessionId, int currentUserId);

        /// <summary>
        /// Lấy danh sách hóa đơn của người dùng
        /// </summary>
        Task<(IEnumerable<InvoiceResponseDto> Items, int Total)> GetUserInvoicesAsync(
            int userId, 
            int skip = 0, 
            int take = 20);

        /// <summary>
        /// Tạo hóa đơn cho phiên sạc đã thanh toán
        /// </summary>
        Task<InvoiceResponseDto> CreateInvoiceForSessionAsync(
            int sessionId,
            int userId,
            decimal amount,
            string paymentMethod,
            string invoiceNumberPrefix = "INV");
    }
}

