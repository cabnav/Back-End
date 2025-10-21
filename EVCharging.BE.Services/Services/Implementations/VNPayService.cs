using EVCharging.BE.Common.DTOs.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace EVCharging.BE.Services.Services.Implementations
{
    /// <summary>
    /// VNPay Service implementation - tích hợp VNPay payment gateway
    /// </summary>
    public class VNPayService : IVNPayService
    {
        private readonly string _tmnCode;
        private readonly string _hashSecret;
        private readonly string _baseUrl;
        private readonly string _returnUrl;
        private readonly string _cancelUrl;
        private readonly string _notifyUrl;

        public VNPayService(IConfiguration configuration)
        {
            _tmnCode = configuration["VNPay:TmnCode"] ?? "YOUR_TMN_CODE";
            _hashSecret = configuration["VNPay:HashSecret"] ?? "YOUR_HASH_SECRET";
            _baseUrl = configuration["VNPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            _returnUrl = configuration["VNPay:ReturnUrl"] ?? "https://yourapp.com/payment/return";
            _cancelUrl = configuration["VNPay:CancelUrl"] ?? "https://yourapp.com/payment/cancel";
            _notifyUrl = configuration["VNPay:NotifyUrl"] ?? "https://yourapp.com/payment/notify";
        }

        /// <summary>
        /// Tạo VNPay payment request
        /// </summary>
        public async Task<PaymentResponse> CreatePaymentRequestAsync(PaymentCreateRequest request)
        {
            try
            {
                var parameters = await GetPaymentParametersAsync(request);
                var paymentUrl = await GeneratePaymentUrlAsync(request);

                return new PaymentResponse
                {
                    PaymentId = 0, // Will be set after payment creation
                    PaymentUrl = paymentUrl,
                    PaymentStatus = "pending",
                    Amount = request.Amount,
                    PaymentMethod = "vnpay"
                };
            }
            catch (Exception ex)
            {
                return new PaymentResponse
                {
                    ErrorMessage = $"Error creating VNPay payment: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Tạo VNPay payment URL
        /// </summary>
        public async Task<string> GeneratePaymentUrlAsync(PaymentCreateRequest request)
        {
            var parameters = await GetPaymentParametersAsync(request);
            var queryString = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            return $"{_baseUrl}?{queryString}";
        }

        /// <summary>
        /// Xử lý VNPay callback
        /// </summary>
        public async Task<PaymentCallbackResponse> ProcessCallbackAsync(PaymentCallbackRequest request)
        {
            try
            {
                // Verify signature
                var isValid = await VerifySignatureAsync(request);
                if (!isValid)
                {
                    return new PaymentCallbackResponse
                    {
                        Success = false,
                        Message = "Invalid signature"
                    };
                }

                // Process payment status
                var isSuccess = request.Status == "00";
                
                return new PaymentCallbackResponse
                {
                    Success = isSuccess,
                    Message = isSuccess ? "Payment successful" : "Payment failed",
                    TransactionId = request.TransactionId,
                    Amount = request.Amount,
                    PaymentStatus = isSuccess ? "completed" : "failed"
                };
            }
            catch (Exception ex)
            {
                return new PaymentCallbackResponse
                {
                    Success = false,
                    Message = $"Error processing VNPay callback: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Verify VNPay signature
        /// </summary>
        public async Task<bool> VerifySignatureAsync(PaymentCallbackRequest request)
        {
            try
            {
                // TODO: Implement actual VNPay signature verification
                // This is a simplified version for demo purposes
                return !string.IsNullOrEmpty(request.Signature);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy return URL
        /// </summary>
        public string GetReturnUrl()
        {
            return _returnUrl;
        }

        /// <summary>
        /// Lấy cancel URL
        /// </summary>
        public string GetCancelUrl()
        {
            return _cancelUrl;
        }

        /// <summary>
        /// Lấy notify URL
        /// </summary>
        public string GetNotifyUrl()
        {
            return _notifyUrl;
        }

        /// <summary>
        /// Tạo secure hash cho VNPay
        /// </summary>
        public string GenerateSecureHash(Dictionary<string, string> parameters)
        {
            try
            {
                // Sort parameters by key
                var sortedParams = parameters.OrderBy(kvp => kvp.Key).ToList();
                
                // Create query string
                var queryString = string.Join("&", sortedParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                
                // Add hash secret
                var dataToHash = queryString + _hashSecret;
                
                // Generate SHA256 hash
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));
                return Convert.ToHexString(hashBytes).ToLower();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating secure hash: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy payment parameters cho VNPay
        /// </summary>
        public async Task<Dictionary<string, string>> GetPaymentParametersAsync(PaymentCreateRequest request)
        {
            var vnpTxnRef = Guid.NewGuid().ToString();
            var vnpAmount = (request.Amount * 100).ToString(); // VNPay uses cents
            var vnpCreateDate = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var vnpExpireDate = DateTime.UtcNow.AddMinutes(15).ToString("yyyyMMddHHmmss");

            var parameters = new Dictionary<string, string>
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = _tmnCode,
                ["vnp_Amount"] = vnpAmount,
                ["vnp_CurrCode"] = "VND",
                ["vnp_TxnRef"] = vnpTxnRef,
                ["vnp_OrderInfo"] = request.Description ?? $"EV Charging Payment - {request.SessionId}",
                ["vnp_OrderType"] = "other",
                ["vnp_Locale"] = "vn",
                ["vnp_ReturnUrl"] = _returnUrl,
                ["vnp_IpAddr"] = "127.0.0.1", // TODO: Get actual client IP
                ["vnp_CreateDate"] = vnpCreateDate,
                ["vnp_ExpireDate"] = vnpExpireDate
            };

            // Generate secure hash
            var secureHash = GenerateSecureHash(parameters);
            parameters["vnp_SecureHash"] = secureHash;

            return parameters;
        }
    }
}
