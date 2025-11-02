using EVCharging.BE.Common.DTOs.Payments;
using Microsoft.Extensions.Options;
using RestSharp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

namespace EVCharging.BE.Services.Services.Payment.Implementations
{
    /// <summary>
    /// Service xử lý thanh toán qua MoMo
    /// </summary>
    public class MomoService : IMomoService
    {
        private readonly IOptions<MomoOptionModel> _options;
        private readonly EvchargingManagementContext _db;
        private readonly IInvoiceService _invoiceService;

        public MomoService(
            IOptions<MomoOptionModel> options,
            EvchargingManagementContext db,
            IInvoiceService invoiceService)
        {
            _options = options;
            _db = db;
            _invoiceService = invoiceService;
        }

        public async Task<MomoCreatePaymentResponseDto> CreatePaymentAsync(MomoCreatePaymentRequestDto model)
        {
            // Tạo OrderId từ SessionId và timestamp để đảm bảo unique
            var orderId = $"{model.SessionId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{model.UserId}";
            var requestId = orderId;

            // Format OrderInfo
            var orderInfo = $"Khách hàng: {model.FullName}. Nội dung: {model.OrderInfo}";

            // Tạo raw data để tính signature
            var rawData =
                $"partnerCode={_options.Value.PartnerCode}" +
                $"&accessKey={_options.Value.AccessKey}" +
                $"&requestId={requestId}" +
                $"&amount={(long)model.Amount}" +
                $"&orderId={orderId}" +
                $"&orderInfo={orderInfo}" +
                $"&returnUrl={_options.Value.ReturnUrl}" +
                $"&notifyUrl={_options.Value.NotifyUrl}" +
                $"&extraData=";

            var signature = ComputeHmacSha256(rawData, _options.Value.SecretKey);

            // Tạo HTTP client và request
            var client = new RestClient(_options.Value.MomoApiUrl);
            var request = new RestRequest() { Method = Method.Post };

            request.AddHeader("Content-Type", "application/json; charset=UTF-8");

            // Tạo request body
            var requestData = new
            {
                accessKey = _options.Value.AccessKey,
                partnerCode = _options.Value.PartnerCode,
                requestType = _options.Value.RequestType,
                notifyUrl = _options.Value.NotifyUrl,
                returnUrl = _options.Value.ReturnUrl,
                orderId = orderId,
                amount = ((long)model.Amount).ToString(),
                orderInfo = orderInfo,
                requestId = requestId,
                extraData = "",
                signature = signature
            };

            // MoMo API yêu cầu exact property names (camelCase) - không dùng PropertyNamingPolicy
            var jsonContent = JsonSerializer.Serialize(requestData);
            request.AddParameter("application/json", jsonContent, ParameterType.RequestBody);

            var response = await client.ExecuteAsync(request);

            if (response.Content == null)
            {
                throw new InvalidOperationException("MoMo API returned null response");
            }

            var momoResponse = JsonSerializer.Deserialize<MomoCreatePaymentResponseDto>(response.Content)
                ?? throw new InvalidOperationException("Failed to deserialize MoMo response");

            return momoResponse;
        }

        public MomoExecuteResponseDto PaymentExecuteAsync(IQueryCollection collection)
        {
            var amount = collection.FirstOrDefault(s => s.Key == "amount").Value.ToString() ?? string.Empty;
            var orderInfo = collection.FirstOrDefault(s => s.Key == "orderInfo").Value.ToString() ?? string.Empty;
            var orderId = collection.FirstOrDefault(s => s.Key == "orderId").Value.ToString() ?? string.Empty;
            var errorCode = collection.FirstOrDefault(s => s.Key == "errorCode").Value.ToString();
            var paymentStatus = collection.FirstOrDefault(s => s.Key == "resultCode").Value.ToString();
            var transactionId = collection.FirstOrDefault(s => s.Key == "transId").Value.ToString();

            return new MomoExecuteResponseDto()
            {
                Amount = amount,
                OrderId = orderId,
                OrderInfo = orderInfo,
                ErrorCode = errorCode,
                PaymentStatus = paymentStatus,
                TransactionId = transactionId
            };
        }

        public bool VerifySignature(IQueryCollection collection, string secretKey)
        {
            var amount = collection.FirstOrDefault(s => s.Key == "amount").Value.ToString() ?? string.Empty;
            var orderInfo = collection.FirstOrDefault(s => s.Key == "orderInfo").Value.ToString() ?? string.Empty;
            var orderId = collection.FirstOrDefault(s => s.Key == "orderId").Value.ToString() ?? string.Empty;
            var requestId = collection.FirstOrDefault(s => s.Key == "requestId").Value.ToString() ?? string.Empty;
            var errorCode = collection.FirstOrDefault(s => s.Key == "errorCode").Value.ToString() ?? string.Empty;
            var transId = collection.FirstOrDefault(s => s.Key == "transId").Value.ToString() ?? string.Empty;
            var resultCode = collection.FirstOrDefault(s => s.Key == "resultCode").Value.ToString() ?? string.Empty;
            var message = collection.FirstOrDefault(s => s.Key == "message").Value.ToString() ?? string.Empty;
            var localMessage = collection.FirstOrDefault(s => s.Key == "localMessage").Value.ToString() ?? string.Empty;
            var partnerCode = collection.FirstOrDefault(s => s.Key == "partnerCode").Value.ToString() ?? string.Empty;
            var extraData = collection.FirstOrDefault(s => s.Key == "extraData").Value.ToString() ?? string.Empty;
            var receivedSignature = collection.FirstOrDefault(s => s.Key == "signature").Value.ToString() ?? string.Empty;

            // Tạo raw data để tính signature
            var rawData =
                $"partnerCode={partnerCode}" +
                $"&accessKey={_options.Value.AccessKey}" +
                $"&requestId={requestId}" +
                $"&amount={amount}" +
                $"&orderId={orderId}" +
                $"&orderInfo={orderInfo}" +
                $"&orderType=" +
                $"&transId={transId}" +
                $"&message={message}" +
                $"&localMessage={localMessage}" +
                $"&responseTime=" +
                $"&errorCode={errorCode}" +
                $"&payType=" +
                $"&extraData={extraData}";

            var computedSignature = ComputeHmacSha256(rawData, secretKey);

            return computedSignature.Equals(receivedSignature, StringComparison.OrdinalIgnoreCase);
        }

        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] hashBytes;

            using (var hmac = new HMACSHA256(keyBytes))
            {
                hashBytes = hmac.ComputeHash(messageBytes);
            }

            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            return hashString;
        }

        public async Task<MomoCallbackResult> ProcessCallbackAsync(IQueryCollection collection)
        {
            try
            {
                var result = PaymentExecuteAsync(collection);

                // Verify signature
                var isValid = VerifySignature(collection, _options.Value.SecretKey);
                if (!isValid)
                {
                    return new MomoCallbackResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid signature",
                        OrderId = result.OrderId
                    };
                }

                // Parse orderId để lấy sessionId
                var orderIdParts = result.OrderId.Split('_');
                if (orderIdParts.Length < 3 || !int.TryParse(orderIdParts[0], out var sessionId))
                {
                    return new MomoCallbackResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid orderId format",
                        OrderId = result.OrderId
                    };
                }

                // Tìm payment record
                var payment = await _db.Payments
                    .FirstOrDefaultAsync(p => p.InvoiceNumber == result.OrderId);

                if (payment == null)
                {
                    return new MomoCallbackResult
                    {
                        Success = false,
                        ErrorMessage = "Payment not found",
                        OrderId = result.OrderId
                    };
                }

                // Kiểm tra đã xử lý chưa
                if (payment.PaymentStatus == "success")
                {
                    return new MomoCallbackResult
                    {
                        Success = true,
                        RedirectUrl = $"/payment/success?orderId={result.OrderId}",
                        OrderId = result.OrderId
                    };
                }

                // Kiểm tra kết quả thanh toán
                if (result.PaymentStatus == "0") // 0 = thành công
                {
                    // Cập nhật payment status
                    payment.PaymentStatus = "success";
                    payment.CreatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();

                    // Tạo invoice nếu chưa có
                    if (payment.SessionId.HasValue)
                    {
                        var existingInvoice = await _db.Invoices
                            .FirstOrDefaultAsync(i => i.InvoiceNumber == payment.InvoiceNumber);

                        if (existingInvoice == null)
                        {
                            var invoice = await _invoiceService.CreateInvoiceForSessionAsync(
                                payment.SessionId.Value,
                                payment.UserId,
                                payment.Amount,
                                "momo"
                            );

                            // Cập nhật InvoiceNumber trong Payment
                            payment.InvoiceNumber = invoice.InvoiceNumber;
                            await _db.SaveChangesAsync();
                        }
                    }

                    return new MomoCallbackResult
                    {
                        Success = true,
                        RedirectUrl = $"/payment/success?orderId={result.OrderId}",
                        OrderId = result.OrderId
                    };
                }
                else
                {
                    // Thanh toán thất bại
                    payment.PaymentStatus = "failed";
                    await _db.SaveChangesAsync();

                    return new MomoCallbackResult
                    {
                        Success = false,
                        RedirectUrl = $"/payment/failed?orderId={result.OrderId}&errorCode={result.ErrorCode}",
                        OrderId = result.OrderId,
                        ErrorCode = result.ErrorCode,
                        ErrorMessage = "Payment failed"
                    };
                }
            }
            catch (Exception ex)
            {
                return new MomoCallbackResult
                {
                    Success = false,
                    ErrorMessage = $"Error processing callback: {ex.Message}",
                    OrderId = PaymentExecuteAsync(collection).OrderId
                };
            }
        }

        public async Task<MomoNotifyResult> ProcessNotifyAsync(IQueryCollection collection)
        {
            try
            {
                var result = PaymentExecuteAsync(collection);

                // Verify signature
                var isValid = VerifySignature(collection, _options.Value.SecretKey);
                if (!isValid)
                {
                    return new MomoNotifyResult
                    {
                        Success = false,
                        Message = "Invalid signature",
                        OrderId = result.OrderId
                    };
                }

                // Parse orderId để lấy sessionId
                var orderIdParts = result.OrderId.Split('_');
                if (orderIdParts.Length < 3 || !int.TryParse(orderIdParts[0], out var sessionId))
                {
                    return new MomoNotifyResult
                    {
                        Success = false,
                        Message = "Invalid orderId format",
                        OrderId = result.OrderId
                    };
                }

                // Tìm payment record
                var payment = await _db.Payments
                    .FirstOrDefaultAsync(p => p.InvoiceNumber == result.OrderId);

                if (payment == null)
                {
                    return new MomoNotifyResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        OrderId = result.OrderId
                    };
                }

                // Kiểm tra đã xử lý chưa
                if (payment.PaymentStatus == "success")
                {
                    return new MomoNotifyResult
                    {
                        Success = true,
                        Message = "Payment already processed",
                        OrderId = result.OrderId,
                        PaymentStatus = payment.PaymentStatus
                    };
                }

                // Kiểm tra kết quả thanh toán
                if (result.PaymentStatus == "0") // 0 = thành công
                {
                    // Cập nhật payment status
                    payment.PaymentStatus = "success";
                    payment.CreatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();

                    // Tạo invoice nếu chưa có
                    if (payment.SessionId.HasValue)
                    {
                        var existingInvoice = await _db.Invoices
                            .FirstOrDefaultAsync(i => i.InvoiceNumber == payment.InvoiceNumber);

                        if (existingInvoice == null)
                        {
                            var invoice = await _invoiceService.CreateInvoiceForSessionAsync(
                                payment.SessionId.Value,
                                payment.UserId,
                                payment.Amount,
                                "momo"
                            );

                            // Cập nhật InvoiceNumber trong Payment
                            payment.InvoiceNumber = invoice.InvoiceNumber;
                            await _db.SaveChangesAsync();
                        }
                    }

                    return new MomoNotifyResult
                    {
                        Success = true,
                        Message = "Processed successfully",
                        OrderId = result.OrderId,
                        PaymentStatus = payment.PaymentStatus
                    };
                }
                else
                {
                    // Thanh toán thất bại
                    payment.PaymentStatus = "failed";
                    await _db.SaveChangesAsync();

                    return new MomoNotifyResult
                    {
                        Success = false,
                        Message = "Payment failed",
                        OrderId = result.OrderId,
                        PaymentStatus = payment.PaymentStatus
                    };
                }
            }
            catch (Exception ex)
            {
                return new MomoNotifyResult
                {
                    Success = false,
                    Message = $"Error processing notify: {ex.Message}",
                    OrderId = PaymentExecuteAsync(collection).OrderId
                };
            }
        }
    }
}
