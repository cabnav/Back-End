using EVCharging.BE.Common.DTOs.Payments;
using Microsoft.AspNetCore.Http;

namespace EVCharging.BE.Services.Services.Payment
{
    /// <summary>
    /// Service xử lý thanh toán qua MoMo
    /// </summary>
    public interface IMomoService
    {
        /// <summary>
        /// Tạo payment URL từ MoMo
        /// </summary>
        Task<MomoCreatePaymentResponseDto> CreatePaymentAsync(MomoCreatePaymentRequestDto model);

        /// <summary>
        /// Xử lý callback từ MoMo sau khi thanh toán
        /// </summary>
        MomoExecuteResponseDto PaymentExecuteAsync(IQueryCollection collection);

        /// <summary>
        /// Xác thực signature từ MoMo callback
        /// </summary>
        bool VerifySignature(IQueryCollection collection, string secretKey);

        /// <summary>
        /// Xử lý callback từ MoMo (Return URL) - Trả về URL để redirect
        /// </summary>
        Task<MomoCallbackResult> ProcessCallbackAsync(IQueryCollection collection);

        /// <summary>
        /// Xử lý notify từ MoMo (IPN) - Trả về kết quả xử lý
        /// </summary>
        Task<MomoNotifyResult> ProcessNotifyAsync(IQueryCollection collection);
    }

    /// <summary>
    /// Kết quả xử lý callback từ MoMo
    /// </summary>
    public class MomoCallbackResult
    {
        public bool Success { get; set; }
        public string? RedirectUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Kết quả xử lý notify từ MoMo
    /// </summary>
    public class MomoNotifyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
    }
}
