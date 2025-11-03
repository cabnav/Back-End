using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Charging;
using EVCharging.BE.Services.Services.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

namespace EVCharging.BE.Services.Services.Payment.Implementations
{
    /// <summary>
    /// Payment Service implementation - quản lý thanh toán
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IUserService _userService;
        private readonly ICostCalculationService _costCalculationService;
        private readonly IVNPayService _vnPayService;
        private readonly IMoMoService _moMoService;

        public PaymentService(
            EvchargingManagementContext db,
            IUserService userService,
            ICostCalculationService costCalculationService,
            IVNPayService vnPayService,
            IMoMoService moMoService)
        {
            _db = db;
            _userService = userService;
            _costCalculationService = costCalculationService;
            _vnPayService = vnPayService;
            _moMoService = moMoService;
        }

        /// <summary>
        /// Tạo payment mới
        /// </summary>
        public async Task<PaymentResponse> CreatePaymentAsync(PaymentCreateRequest request)
        {
            try
            {
                // Validate request
                if (!await ValidatePaymentRequestAsync(request))
                {
                    return new PaymentResponse
                    {
                        ErrorMessage = "Invalid payment request"
                    };
                }

                // Tạo payment entity
                var payment = new PaymentEntity
                {
                    UserId = request.UserId,
                    SessionId = request.SessionId,
                    ReservationId = request.ReservationId,
                    Amount = request.Amount,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = "pending",
                    InvoiceNumber = await GenerateInvoiceNumberAsync(),
                    CreatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                // Process payment based on method
                PaymentResponse response = request.PaymentMethod.ToLower() switch
                {
                    "wallet" => await ProcessWalletPaymentAsync(request),
                    "vnpay" => await ProcessVNPayPaymentAsync(request),
                    "momo" => await ProcessMoMoPaymentAsync(request),
                    "cash" => new PaymentResponse
                    {
                        PaymentId = payment.PaymentId,
                        UserId = payment.UserId,
                        SessionId = payment.SessionId,
                        Amount = payment.Amount,
                        PaymentMethod = payment.PaymentMethod ?? "",
                        PaymentStatus = payment.PaymentStatus ?? "pending",
                        InvoiceNumber = payment.InvoiceNumber,
                        CreatedAt = payment.CreatedAt ?? DateTime.UtcNow
                        // Status remains "pending" until staff confirms cash payment
                    },
                    "card" => new PaymentResponse
                    {
                        PaymentId = payment.PaymentId,
                        UserId = payment.UserId,
                        SessionId = payment.SessionId,
                        Amount = payment.Amount,
                        PaymentMethod = payment.PaymentMethod ?? "",
                        PaymentStatus = payment.PaymentStatus ?? "pending",
                        InvoiceNumber = payment.InvoiceNumber,
                        CreatedAt = payment.CreatedAt ?? DateTime.UtcNow
                        // Status remains "pending" until staff confirms card payment via POS
                    },
                    "pos" => new PaymentResponse
                    {
                        PaymentId = payment.PaymentId,
                        UserId = payment.UserId,
                        SessionId = payment.SessionId,
                        Amount = payment.Amount,
                        PaymentMethod = payment.PaymentMethod ?? "",
                        PaymentStatus = payment.PaymentStatus ?? "pending",
                        InvoiceNumber = payment.InvoiceNumber,
                        CreatedAt = payment.CreatedAt ?? DateTime.UtcNow
                        // Status remains "pending" until staff confirms POS payment
                    },
                    _ => new PaymentResponse
                    {
                        PaymentId = payment.PaymentId,
                        ErrorMessage = "Unsupported payment method"
                    }
                };

                return response;
            }
            catch (Exception ex)
            {
                return new PaymentResponse
                {
                    ErrorMessage = $"Error creating payment: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Lấy payment theo ID
        /// </summary>
        public async Task<PaymentResponse> GetPaymentByIdAsync(int paymentId)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                return new PaymentResponse { ErrorMessage = "Payment not found" };

            return MapToPaymentResponse(payment);
        }

        /// <summary>
        /// Lấy payments theo user
        /// </summary>
        public async Task<IEnumerable<PaymentResponse>> GetPaymentsByUserAsync(int userId, int page = 1, int pageSize = 50)
        {
            var payments = await _db.Payments
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return payments.Select(MapToPaymentResponse);
        }

        /// <summary>
        /// Lấy payments theo session
        /// </summary>
        public async Task<IEnumerable<PaymentResponse>> GetPaymentsBySessionAsync(int sessionId)
        {
            var payments = await _db.Payments
                .Where(p => p.SessionId == sessionId)
                .ToListAsync();

            return payments.Select(MapToPaymentResponse);
        }

        /// <summary>
        /// Cập nhật trạng thái payment
        /// </summary>
        public async Task<PaymentResponse> UpdatePaymentStatusAsync(int paymentId, string status, string? transactionId = null)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                return new PaymentResponse { ErrorMessage = "Payment not found" };

            payment.PaymentStatus = status;
            if (!string.IsNullOrEmpty(transactionId))
            {
                // Store external transaction ID in a custom field or separate table
                // For now, we'll use the InvoiceNumber field to store it
                payment.InvoiceNumber = transactionId;
            }

            if (status == "completed")
            {
                // Update user wallet if needed
                if (payment.PaymentMethod == "wallet")
                {
                    await _userService.WalletTopUpAsync(payment.UserId, -payment.Amount, $"Payment for session {payment.SessionId}");
                }
            }

            await _db.SaveChangesAsync();
            return MapToPaymentResponse(payment);
        }

        /// <summary>
        /// Xử lý thanh toán VNPay
        /// </summary>
        public async Task<PaymentResponse> ProcessVNPayPaymentAsync(PaymentCreateRequest request)
        {
            try
            {
                return await _vnPayService.CreatePaymentRequestAsync(request);
            }
            catch (Exception ex)
            {
                return new PaymentResponse
                {
                    ErrorMessage = $"Error processing VNPay payment: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Xử lý thanh toán MoMo
        /// </summary>
        public async Task<PaymentResponse> ProcessMoMoPaymentAsync(PaymentCreateRequest request)
        {
            try
            {
                return await _moMoService.CreatePaymentRequestAsync(request);
            }
            catch (Exception ex)
            {
                return new PaymentResponse
                {
                    ErrorMessage = $"Error processing MoMo payment: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Xử lý callback từ payment gateway
        /// </summary>
        public async Task<PaymentCallbackResponse> HandlePaymentCallbackAsync(PaymentCallbackRequest request, string gateway)
        {
            try
            {
                var payment = await _db.Payments
                    .FirstOrDefaultAsync(p => p.PaymentId.ToString() == request.PaymentId);

                if (payment == null)
                {
                    return new PaymentCallbackResponse
                    {
                        Success = false,
                        Message = "Payment not found"
                    };
                }

                // Process callback based on gateway
                PaymentCallbackResponse result = gateway.ToLower() switch
                {
                    "vnpay" => await _vnPayService.ProcessCallbackAsync(request),
                    "momo" => await _moMoService.ProcessCallbackAsync(request),
                    _ => new PaymentCallbackResponse
                    {
                        Success = false,
                        Message = "Unsupported payment gateway"
                    }
                };

                if (result.Success)
                {
                    // Update payment status
                    payment.PaymentStatus = "completed";
                    await _db.SaveChangesAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                return new PaymentCallbackResponse
                {
                    Success = false,
                    Message = $"Error processing callback: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Xử lý thanh toán bằng ví
        /// </summary>
        public async Task<PaymentResponse> ProcessWalletPaymentAsync(PaymentCreateRequest request)
        {
            try
            {
                // Check wallet balance
                if (!await ValidateWalletBalanceAsync(request.UserId, request.Amount))
                {
                    return new PaymentResponse
                    {
                        ErrorMessage = "Insufficient wallet balance"
                    };
                }

                // Deduct from wallet
                await _userService.WalletTopUpAsync(request.UserId, -request.Amount, $"Payment for session {request.SessionId}");

                return new PaymentResponse
                {
                    PaymentId = 0, // Will be set after payment creation
                    PaymentStatus = "completed",
                    CompletedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new PaymentResponse
                {
                    ErrorMessage = $"Error processing wallet payment: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Kiểm tra số dư ví
        /// </summary>
        public async Task<bool> ValidateWalletBalanceAsync(int userId, decimal amount)
        {
            var user = await _userService.GetByIdAsync(userId);
            return user?.WalletBalance >= amount;
        }

        /// <summary>
        /// Xử lý hoàn tiền
        /// </summary>
        public async Task<RefundResponse> ProcessRefundAsync(RefundRequest request)
        {
            try
            {
                var payment = await _db.Payments
                    .FirstOrDefaultAsync(p => p.PaymentId == request.PaymentId);

                if (payment == null)
                {
                    return new RefundResponse
                    {
                        ErrorMessage = "Payment not found"
                    };
                }

                if (!await CanProcessRefundAsync(request.PaymentId, request.Amount))
                {
                    return new RefundResponse
                    {
                        ErrorMessage = "Cannot process refund"
                    };
                }

                // Create refund record (you might need a Refund entity)
                // For now, we'll create a negative wallet transaction
                if (payment.PaymentMethod == "wallet")
                {
                    await _userService.WalletTopUpAsync(payment.UserId, request.Amount, $"Refund for payment {request.PaymentId}: {request.Reason}");
                }

                return new RefundResponse
                {
                    RefundId = 0, // Would be set after creating refund record
                    PaymentId = request.PaymentId,
                    Amount = request.Amount,
                    Status = "completed",
                    Reason = request.Reason,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new RefundResponse
                {
                    ErrorMessage = $"Error processing refund: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Lấy refunds theo payment
        /// </summary>
        public async Task<IEnumerable<RefundResponse>> GetRefundsByPaymentAsync(int paymentId)
        {
            // TODO: Implement when Refund entity is created
            return new List<RefundResponse>();
        }

        /// <summary>
        /// Tạo số hóa đơn
        /// </summary>
        public async Task<string> GenerateInvoiceNumberAsync()
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var count = await _db.Payments
                .Where(p => p.CreatedAt.Value.Date == DateTime.UtcNow.Date)
                .CountAsync();

            return $"INV-{today}-{count + 1:D4}";
        }

        /// <summary>
        /// Tạo hóa đơn
        /// </summary>
        public async Task<PaymentResponse> GenerateInvoiceAsync(int paymentId)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                return new PaymentResponse { ErrorMessage = "Payment not found" };

            // TODO: Generate actual invoice PDF
            payment.InvoiceNumber = await GenerateInvoiceNumberAsync();
            await _db.SaveChangesAsync();

            return MapToPaymentResponse(payment);
        }

        /// <summary>
        /// Lấy analytics thanh toán
        /// </summary>
        public async Task<Dictionary<string, object>> GetPaymentAnalyticsAsync(DateTime from, DateTime to)
        {
            var payments = await _db.Payments
                .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
                .ToListAsync();

            return new Dictionary<string, object>
            {
                ["totalPayments"] = payments.Count,
                ["totalAmount"] = payments.Sum(p => p.Amount),
                ["successfulPayments"] = payments.Count(p => p.PaymentStatus == "completed"),
                ["failedPayments"] = payments.Count(p => p.PaymentStatus == "failed"),
                ["averageAmount"] = payments.Any() ? payments.Average(p => p.Amount) : 0
            };
        }

        /// <summary>
        /// Lấy tổng doanh thu
        /// </summary>
        public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to)
        {
            return await _db.Payments
                .Where(p => p.CreatedAt >= from && p.CreatedAt <= to && p.PaymentStatus == "completed")
                .SumAsync(p => p.Amount);
        }

        /// <summary>
        /// Lấy thống kê phương thức thanh toán
        /// </summary>
        public async Task<Dictionary<string, int>> GetPaymentMethodStatsAsync(DateTime from, DateTime to)
        {
            return await _db.Payments
                .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
                .GroupBy(p => p.PaymentMethod)
                .ToDictionaryAsync(g => g.Key ?? "unknown", g => g.Count());
        }

        /// <summary>
        /// Validate payment request
        /// </summary>
        public async Task<bool> ValidatePaymentRequestAsync(PaymentCreateRequest request)
        {
            if (request.Amount <= 0) return false;
            if (string.IsNullOrEmpty(request.PaymentMethod)) return false;

            var user = await _userService.GetByIdAsync(request.UserId);
            if (user is null) return false;

            return true;
        }

        /// <summary>
        /// Kiểm tra có thể hoàn tiền không
        /// </summary>
        public async Task<bool> CanProcessRefundAsync(int paymentId, decimal amount)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null) return false;
            if (payment.PaymentStatus != "completed") return false;
            if (amount > payment.Amount) return false;

            return true;
        }

        // Helper methods
        private PaymentResponse MapToPaymentResponse(PaymentEntity payment)
        {
            return new PaymentResponse
            {
                PaymentId = payment.PaymentId,
                UserId = payment.UserId,
                SessionId = payment.SessionId,
                ReservationId = payment.ReservationId,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod ?? "",
                PaymentStatus = payment.PaymentStatus ?? "",
                InvoiceNumber = payment.InvoiceNumber,
                CreatedAt = payment.CreatedAt ?? DateTime.UtcNow
            };
        }

        private async Task<bool> VerifyVNPaySignatureAsync(PaymentCallbackRequest request)
        {
            // TODO: Implement VNPay signature verification
            return true;
        }

        private async Task<bool> VerifyMoMoSignatureAsync(PaymentCallbackRequest request)
        {
            // TODO: Implement MoMo signature verification
            return true;
        }
    }
}
