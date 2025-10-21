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
    /// MoMo Service implementation - tích hợp MoMo payment gateway
    /// </summary>
    public class MoMoService : IMoMoService
    {
        private readonly string _partnerCode;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _baseUrl;
        private readonly string _returnUrl;
        private readonly string _notifyUrl;

        public MoMoService(IConfiguration configuration)
        {
            _partnerCode = configuration["MoMo:PartnerCode"] ?? "YOUR_PARTNER_CODE";
            _accessKey = configuration["MoMo:AccessKey"] ?? "YOUR_ACCESS_KEY";
            _secretKey = configuration["MoMo:SecretKey"] ?? "YOUR_SECRET_KEY";
            _baseUrl = configuration["MoMo:BaseUrl"] ?? "https://test-payment.momo.vn/v2/gateway/pay";
            _returnUrl = configuration["MoMo:ReturnUrl"] ?? "https://yourapp.com/payment/return";
            _notifyUrl = configuration["MoMo:NotifyUrl"] ?? "https://yourapp.com/payment/notify";
        }

        /// <summary>
        /// Tạo MoMo payment request
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
                    PaymentMethod = "momo"
                };
            }
            catch (Exception ex)
            {
                return new PaymentResponse
                {
                    ErrorMessage = $"Error creating MoMo payment: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Tạo MoMo payment URL
        /// </summary>
        public async Task<string> GeneratePaymentUrlAsync(PaymentCreateRequest request)
        {
            var parameters = await GetPaymentParametersAsync(request);
            var queryString = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            return $"{_baseUrl}?{queryString}";
        }

        /// <summary>
        /// Xử lý MoMo callback
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
                    Message = $"Error processing MoMo callback: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Verify MoMo signature
        /// </summary>
        public async Task<bool> VerifySignatureAsync(PaymentCallbackRequest request)
        {
            try
            {
                // TODO: Implement actual MoMo signature verification
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
        /// Lấy notify URL
        /// </summary>
        public string GetNotifyUrl()
        {
            return _notifyUrl;
        }

        /// <summary>
        /// Tạo signature cho MoMo
        /// </summary>
        public string GenerateSignature(Dictionary<string, string> parameters)
        {
            try
            {
                // Sort parameters by key
                var sortedParams = parameters.OrderBy(kvp => kvp.Key).ToList();
                
                // Create query string
                var queryString = string.Join("&", sortedParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                
                // Add secret key
                var dataToSign = queryString + _secretKey;
                
                // Generate HMAC SHA256 signature
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
                var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
                return Convert.ToHexString(signatureBytes).ToLower();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating MoMo signature: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy payment parameters cho MoMo
        /// </summary>
        public async Task<Dictionary<string, string>> GetPaymentParametersAsync(PaymentCreateRequest request)
        {
            var requestId = Guid.NewGuid().ToString();
            var orderId = Guid.NewGuid().ToString();
            var amount = request.Amount.ToString();
            var orderInfo = request.Description ?? $"EV Charging Payment - {request.SessionId}";
            var extraData = "";

            var parameters = new Dictionary<string, string>
            {
                ["partnerCode"] = _partnerCode,
                ["accessKey"] = _accessKey,
                ["requestId"] = requestId,
                ["amount"] = amount,
                ["orderId"] = orderId,
                ["orderInfo"] = orderInfo,
                ["returnUrl"] = _returnUrl,
                ["notifyUrl"] = _notifyUrl,
                ["extraData"] = extraData
            };

            // Generate signature
            var signature = GenerateSignature(parameters);
            parameters["signature"] = signature;

            return parameters;
        }
    }
}
