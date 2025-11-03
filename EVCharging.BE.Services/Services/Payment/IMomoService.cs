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
        Task<MomoCallbackResultDto> ProcessCallbackAsync(IQueryCollection collection);

        /// <summary>
        /// Xử lý notify từ MoMo (IPN) - Trả về kết quả xử lý
        /// </summary>
        Task<MomoNotifyResultDto> ProcessNotifyAsync(IQueryCollection collection);
    }
}
