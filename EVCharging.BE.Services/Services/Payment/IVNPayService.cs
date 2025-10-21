using EVCharging.BE.Common.DTOs.Payments;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Payment
{
    /// <summary>
    /// Interface cho VNPay Service - tích hợp VNPay payment gateway
    /// </summary>
    public interface IVNPayService
    {
        // Payment Creation
        Task<PaymentResponse> CreatePaymentRequestAsync(PaymentCreateRequest request);
        Task<string> GeneratePaymentUrlAsync(PaymentCreateRequest request);
        
        // Callback Handling
        Task<PaymentCallbackResponse> ProcessCallbackAsync(PaymentCallbackRequest request);
        Task<bool> VerifySignatureAsync(PaymentCallbackRequest request);
        
        // Configuration
        string GetReturnUrl();
        string GetCancelUrl();
        string GetNotifyUrl();
        
        // Utility
        string GenerateSecureHash(Dictionary<string, string> parameters);
        Task<Dictionary<string, string>> GetPaymentParametersAsync(PaymentCreateRequest request);
    }
}
