using EVCharging.BE.Common.DTOs.Payments;
using Microsoft.Extensions.Options;
using RestSharp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;

        public MomoService(
            IOptions<MomoOptionModel> options,
            EvchargingManagementContext db,
            IInvoiceService invoiceService,
            IConfiguration configuration)
        {
            _options = options;
            _db = db;
            _invoiceService = invoiceService;
            _configuration = configuration;
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
            var orderType = collection.FirstOrDefault(s => s.Key == "orderType").Value.ToString() ?? string.Empty;
            var responseTime = collection.FirstOrDefault(s => s.Key == "responseTime").Value.ToString() ?? string.Empty;
            var payType = collection.FirstOrDefault(s => s.Key == "payType").Value.ToString() ?? string.Empty;
            var receivedSignature = collection.FirstOrDefault(s => s.Key == "signature").Value.ToString() ?? string.Empty;

            // Tạo raw data để tính signature theo đúng format MoMo yêu cầu
            // Lưu ý: MoMo sử dụng giá trị từ URL query params (có thể đã được URL decode bởi ASP.NET)
            var rawData =
                $"partnerCode={partnerCode}" +
                $"&accessKey={_options.Value.AccessKey}" +
                $"&requestId={requestId}" +
                $"&amount={amount}" +
                $"&orderId={orderId}" +
                $"&orderInfo={orderInfo}" +
                $"&orderType={orderType}" +
                $"&transId={transId}" +
                $"&message={message}" +
                $"&localMessage={localMessage}" +
                $"&responseTime={responseTime}" +
                $"&errorCode={errorCode}" +
                $"&payType={payType}" +
                $"&extraData={extraData}";

            var computedSignature = ComputeHmacSha256(rawData, secretKey);

            // Log để debug
            Console.WriteLine($"🔐 Verify Signature:");
            Console.WriteLine($"Raw Data: {rawData}");
            Console.WriteLine($"Computed Signature: {computedSignature}");
            Console.WriteLine($"Received Signature: {receivedSignature}");
            Console.WriteLine($"Match: {computedSignature.Equals(receivedSignature, StringComparison.OrdinalIgnoreCase)}");

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

        public async Task<MomoCallbackResultDto> ProcessCallbackAsync(IQueryCollection collection)
        {
            try
            {
                var result = PaymentExecuteAsync(collection);

                // Log để debug - log tất cả query params để kiểm tra
                Console.WriteLine($"MoMo Callback - OrderId: {result.OrderId}, ResultCode (PaymentStatus): {result.PaymentStatus}, ErrorCode: {result.ErrorCode}, Amount: {result.Amount}");
                Console.WriteLine($"All Query Params: {string.Join(", ", collection.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

                // Verify signature
                var isValid = VerifySignature(collection, _options.Value.SecretKey);
                if (!isValid)
                {
                    var receivedSig = collection.FirstOrDefault(s => s.Key == "signature").Value.ToString() ?? "none";
                    Console.WriteLine($"⚠️ Signature verification FAILED for OrderId: {result.OrderId}, Received signature: {receivedSig}");

                    return new MomoCallbackResultDto
                    {
                        Success = false,
                        ErrorMessage = "Invalid signature",
                        OrderId = result.OrderId
                    };
                }

                Console.WriteLine($"✅ Signature verification PASSED for OrderId: {result.OrderId}");

                // Parse orderId để lấy sessionId
                var orderIdParts = result.OrderId.Split('_');
                if (orderIdParts.Length < 3 || !int.TryParse(orderIdParts[0], out var sessionId))
                {
                    return new MomoCallbackResultDto
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
                    return new MomoCallbackResultDto
                    {
                        Success = false,
                        ErrorMessage = "Payment not found",
                        OrderId = result.OrderId
                    };
                }

                // Kiểm tra đã xử lý chưa
                if (payment.PaymentStatus == "success")
                {
                    // Lấy FrontendUrl từ config
                    var frontendUrl = _configuration["AppSettings:FrontendUrl"]
                        ?? _configuration["AppSettings:BaseUrl"]
                        ?? "http://localhost:5172";

                    return new MomoCallbackResultDto
                    {
                        Success = true,
                        RedirectUrl = $"{frontendUrl}/payment/success?orderId={result.OrderId}",
                        OrderId = result.OrderId
                    };
                }

                // Kiểm tra kết quả thanh toán
                // MoMo trả về: 
                // - resultCode = "0" (thành công) 
                // - hoặc errorCode = "0" (không có lỗi = thành công)
                // Nếu không có resultCode trong URL thì kiểm tra errorCode
                var isSuccess = (!string.IsNullOrEmpty(result.PaymentStatus) && result.PaymentStatus == "0") ||
                               (!string.IsNullOrEmpty(result.ErrorCode) && result.ErrorCode == "0");

                if (isSuccess)
                {
                    Console.WriteLine($"💰 Callback: Payment SUCCESS - OrderId: {result.OrderId}, PaymentId: {payment.PaymentId}, SessionId: {payment.SessionId}, ResultCode: {result.PaymentStatus}, ErrorCode: {result.ErrorCode}");

                    // Cập nhật payment status
                    payment.PaymentStatus = "success";
                    payment.CreatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();
                    Console.WriteLine($"✅ Callback: Payment status updated to 'success' for PaymentId: {payment.PaymentId}, SessionId: {payment.SessionId}");

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
                            Console.WriteLine($"✅ Callback: Invoice created - InvoiceNumber: {invoice.InvoiceNumber}, SessionId: {payment.SessionId}");
                        }
                    }

                    // Lấy FrontendUrl từ config, nếu không có thì dùng BaseUrl
                    var frontendUrl = _configuration["AppSettings:FrontendUrl"]
                        ?? _configuration["AppSettings:BaseUrl"]
                        ?? "http://localhost:5172";

                    return new MomoCallbackResultDto
                    {
                        Success = true,
                        RedirectUrl = $"{frontendUrl}/payment/success?orderId={result.OrderId}",
                        OrderId = result.OrderId
                    };
                }
                else
                {
                    // Thanh toán thất bại
                    payment.PaymentStatus = "failed";
                    await _db.SaveChangesAsync();

                    // Lấy FrontendUrl từ config, nếu không có thì dùng BaseUrl
                    var frontendUrl = _configuration["AppSettings:FrontendUrl"]
                        ?? _configuration["AppSettings:BaseUrl"]
                        ?? "http://localhost:5172";

                    return new MomoCallbackResultDto
                    {
                        Success = false,
                        RedirectUrl = $"{frontendUrl}/payment/failed?orderId={result.OrderId}&errorCode={result.ErrorCode}",
                        OrderId = result.OrderId,
                        ErrorCode = result.ErrorCode,
                        ErrorMessage = "Payment failed"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessCallbackAsync: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                return new MomoCallbackResultDto
                {
                    Success = false,
                    ErrorMessage = $"Error processing callback: {ex.Message}",
                    OrderId = PaymentExecuteAsync(collection).OrderId
                };
            }
        }

        public async Task<MomoNotifyResultDto> ProcessNotifyAsync(IQueryCollection collection)
        {
            try
            {
                var result = PaymentExecuteAsync(collection);

                // Log để debug - log tất cả query params để kiểm tra
                Console.WriteLine($"MoMo Notify - OrderId: {result.OrderId}, ResultCode (PaymentStatus): {result.PaymentStatus}, ErrorCode: {result.ErrorCode}, Amount: {result.Amount}");
                Console.WriteLine($"All Query Params: {string.Join(", ", collection.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

                // Verify signature
                var isValid = VerifySignature(collection, _options.Value.SecretKey);
                if (!isValid)
                {
                    var receivedSig = collection.FirstOrDefault(s => s.Key == "signature").Value.ToString() ?? "none";
                    Console.WriteLine($"⚠️ Notify: Signature verification FAILED for OrderId: {result.OrderId}, Received signature: {receivedSig}");

                    return new MomoNotifyResultDto
                    {
                        Success = false,
                        Message = "Invalid signature",
                        OrderId = result.OrderId
                    };
                }

                Console.WriteLine($"✅ Notify: Signature verification PASSED for OrderId: {result.OrderId}");

                // Parse orderId để lấy sessionId
                var orderIdParts = result.OrderId.Split('_');
                if (orderIdParts.Length < 3 || !int.TryParse(orderIdParts[0], out var sessionId))
                {
                    return new MomoNotifyResultDto
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
                    return new MomoNotifyResultDto
                    {
                        Success = false,
                        Message = "Payment not found",
                        OrderId = result.OrderId
                    };
                }

                // Kiểm tra đã xử lý chưa
                if (payment.PaymentStatus == "success")
                {
                    return new MomoNotifyResultDto
                    {
                        Success = true,
                        Message = "Payment already processed",
                        OrderId = result.OrderId,
                        PaymentStatus = payment.PaymentStatus
                    };
                }

                // Kiểm tra kết quả thanh toán
                // MoMo trả về: 
                // - resultCode = "0" (thành công) 
                // - hoặc errorCode = "0" (không có lỗi = thành công)
                var isSuccess = (!string.IsNullOrEmpty(result.PaymentStatus) && result.PaymentStatus == "0") ||
                               (!string.IsNullOrEmpty(result.ErrorCode) && result.ErrorCode == "0");

                if (isSuccess)
                {
                    Console.WriteLine($"💰 Notify: Payment SUCCESS - OrderId: {result.OrderId}, PaymentId: {payment.PaymentId}, SessionId: {payment.SessionId}, ResultCode: {result.PaymentStatus}, ErrorCode: {result.ErrorCode}");

                    // Cập nhật payment status
                    payment.PaymentStatus = "success";
                    payment.CreatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();
                    Console.WriteLine($"✅ Notify: Payment status updated to 'success' for PaymentId: {payment.PaymentId}, SessionId: {payment.SessionId}");

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
                            Console.WriteLine($"✅ Notify: Invoice created - InvoiceNumber: {invoice.InvoiceNumber}, SessionId: {payment.SessionId}");
                        }
                    }

                    return new MomoNotifyResultDto
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

                    return new MomoNotifyResultDto
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
                return new MomoNotifyResultDto
                {
                    Success = false,
                    Message = $"Error processing notify: {ex.Message}",
                    OrderId = PaymentExecuteAsync(collection).OrderId
                };
            }
        }
    }
}
