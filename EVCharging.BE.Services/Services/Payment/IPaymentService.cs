using EVCharging.BE.Common.DTOs.Payments;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Payment
{
    /// <summary>
    /// Interface cho Payment Service - quản lý thanh toán
    /// </summary>
    public interface IPaymentService
    {
        // Core Payment Operations
        Task<PaymentResponse> CreatePaymentAsync(PaymentCreateRequest request);
        Task<PaymentResponse> GetPaymentByIdAsync(int paymentId);
        Task<IEnumerable<PaymentResponse>> GetPaymentsByUserAsync(int userId, int page = 1, int pageSize = 50);
        Task<IEnumerable<PaymentResponse>> GetPaymentsBySessionAsync(int sessionId);
        Task<PaymentResponse> UpdatePaymentStatusAsync(int paymentId, string status, string? transactionId = null);

        // Payment Gateway Integration
        Task<PaymentResponse> ProcessVNPayPaymentAsync(PaymentCreateRequest request);
        Task<PaymentResponse> ProcessMoMoPaymentAsync(PaymentCreateRequest request);
        Task<PaymentCallbackResponse> HandlePaymentCallbackAsync(PaymentCallbackRequest request, string gateway);

        // Wallet Operations
        Task<PaymentResponse> ProcessWalletPaymentAsync(PaymentCreateRequest request);
        Task<bool> ValidateWalletBalanceAsync(int userId, decimal amount);

        // Refund Operations
        Task<RefundResponse> ProcessRefundAsync(RefundRequest request);
        Task<IEnumerable<RefundResponse>> GetRefundsByPaymentAsync(int paymentId);

        // Invoice Generation
        Task<string> GenerateInvoiceNumberAsync();
        Task<PaymentResponse> GenerateInvoiceAsync(int paymentId);

        // Payment Analytics
        Task<Dictionary<string, object>> GetPaymentAnalyticsAsync(DateTime from, DateTime to);
        Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to);
        Task<Dictionary<string, int>> GetPaymentMethodStatsAsync(DateTime from, DateTime to);

        // Validation
        Task<bool> ValidatePaymentRequestAsync(PaymentCreateRequest request);
        Task<bool> CanProcessRefundAsync(int paymentId, decimal amount);

        // Session Payment Methods
        Task<PaymentResultDto> PayByWalletAsync(int sessionId, int userId);
        Task<PaymentResultDto> PayByCashAsync(int sessionId, int userId);
    }
}
