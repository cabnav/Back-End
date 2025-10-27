using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Services.Services.Payment;
using EVCharging.BE.Services.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserEntity = EVCharging.BE.DAL.Entities.User;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller quản lý thanh toán - Payment Management
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IUserService _userService;

        public PaymentsController(IPaymentService paymentService, IUserService userService)
        {
            _paymentService = paymentService;
            _userService = userService;
        }

        // ====== CORE PAYMENT OPERATIONS ======

        /// <summary>
        /// Tạo payment mới
        /// POST /api/payments
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePayment([FromBody] PaymentCreateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                // Lấy userId từ JWT token
                var userId = int.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                request.UserId = userId;

                var result = await _paymentService.CreatePaymentAsync(request);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                return CreatedAtAction(nameof(GetPayment), new { id = result.PaymentId }, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating payment", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin payment theo ID
        /// GET /api/payments/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPayment(int id)
        {
            try
            {
                var userId = int.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var payment = await _paymentService.GetPaymentByIdAsync(id);

                if (payment == null || string.IsNullOrEmpty(payment.ErrorMessage))
                {
                    return NotFound(new { message = "Payment not found" });
                }

                // Kiểm tra quyền truy cập
                if (payment.UserId != userId && !this.User.IsInRole("Admin"))
                {
                    return Forbid("You don't have permission to view this payment");
                }

                return Ok(payment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving payment", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách payments của user hiện tại
        /// GET /api/payments/my-payments?page=1&pageSize=50
        /// </summary>
        [HttpGet("my-payments")]
        public async Task<IActionResult> GetMyPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = int.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var payments = await _paymentService.GetPaymentsByUserAsync(userId, page, pageSize);
                return Ok(payments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving payments", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy payments theo session
        /// GET /api/payments/by-session/{sessionId}
        /// </summary>
        [HttpGet("by-session/{sessionId:int}")]
        public async Task<IActionResult> GetPaymentsBySession(int sessionId)
        {
            try
            {
                var payments = await _paymentService.GetPaymentsBySessionAsync(sessionId);
                return Ok(payments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving session payments", error = ex.Message });
            }
        }

        // ====== PAYMENT GATEWAY INTEGRATION ======

        /// <summary>
        /// Tạo VNPay payment
        /// POST /api/payments/vnpay
        /// </summary>
        [HttpPost("vnpay")]
        public async Task<IActionResult> CreateVNPayPayment([FromBody] PaymentCreateRequest request)
        {
            try
            {
                var userId = int.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                request.UserId = userId;

                var result = await _paymentService.ProcessVNPayPaymentAsync(request);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating VNPay payment", error = ex.Message });
            }
        }

        /// <summary>
        /// Tạo MoMo payment
        /// POST /api/payments/momo
        /// </summary>
        [HttpPost("momo")]
        public async Task<IActionResult> CreateMoMoPayment([FromBody] PaymentCreateRequest request)
        {
            try
            {
                var userId = int.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                request.UserId = userId;

                var result = await _paymentService.ProcessMoMoPaymentAsync(request);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating MoMo payment", error = ex.Message });
            }
        }

        /// <summary>
        /// Xử lý callback từ payment gateway
        /// POST /api/payments/callback/{gateway}
        /// </summary>
        [HttpPost("callback/{gateway}")]
        [AllowAnonymous] // Payment gateway sẽ gọi endpoint này
        public async Task<IActionResult> HandlePaymentCallback(string gateway, [FromBody] PaymentCallbackRequest request)
        {
            try
            {
                var result = await _paymentService.HandlePaymentCallbackAsync(request, gateway);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing payment callback", error = ex.Message });
            }
        }

        // ====== WALLET OPERATIONS ======

        /// <summary>
        /// Thanh toán bằng ví điện tử
        /// POST /api/payments/wallet
        /// </summary>
        [HttpPost("wallet")]
        public async Task<IActionResult> PayWithWallet([FromBody] PaymentCreateRequest request)
        {
            try
            {
                var userId = int.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                request.UserId = userId;
                request.PaymentMethod = "wallet";

                var result = await _paymentService.ProcessWalletPaymentAsync(request);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing wallet payment", error = ex.Message });
            }
        }

        /// <summary>
        /// Kiểm tra số dư ví
        /// GET /api/payments/wallet/balance
        /// </summary>
        [HttpGet("wallet/balance")]
        public async Task<IActionResult> GetWalletBalance()
        {
            try
            {
                var userId = int.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var userEntity = await _userService.GetByIdAsync(userId);

                if (userEntity == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(new { balance = userEntity.WalletBalance ?? 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving wallet balance", error = ex.Message });
            }
        }

        // ====== REFUND OPERATIONS ======

        /// <summary>
        /// Tạo yêu cầu hoàn tiền
        /// POST /api/payments/refund
        /// </summary>
        [HttpPost("refund")]
        public async Task<IActionResult> ProcessRefund([FromBody] RefundRequest request)
        {
            try
            {
                var result = await _paymentService.ProcessRefundAsync(request);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing refund", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách hoàn tiền theo payment
        /// GET /api/payments/{paymentId}/refunds
        /// </summary>
        [HttpGet("{paymentId:int}/refunds")]
        public async Task<IActionResult> GetRefunds(int paymentId)
        {
            try
            {
                var refunds = await _paymentService.GetRefundsByPaymentAsync(paymentId);
                return Ok(refunds);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving refunds", error = ex.Message });
            }
        }

        // ====== INVOICE OPERATIONS ======

        /// <summary>
        /// Tạo hóa đơn điện tử
        /// POST /api/payments/{paymentId}/invoice
        /// </summary>
        [HttpPost("{paymentId:int}/invoice")]
        public async Task<IActionResult> GenerateInvoice(int paymentId)
        {
            try
            {
                var result = await _paymentService.GenerateInvoiceAsync(paymentId);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while generating invoice", error = ex.Message });
            }
        }

        // ====== ANALYTICS (Admin only) ======

        /// <summary>
        /// Lấy analytics thanh toán (Admin only)
        /// GET /api/payments/analytics?from=2023-01-01&to=2023-12-31
        /// </summary>
        [HttpGet("analytics")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPaymentAnalytics([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
                var toDate = to ?? DateTime.UtcNow;

                var analytics = await _paymentService.GetPaymentAnalyticsAsync(fromDate, toDate);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving payment analytics", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy tổng doanh thu (Admin only)
        /// GET /api/payments/revenue?from=2023-01-01&to=2023-12-31
        /// </summary>
        [HttpGet("revenue")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTotalRevenue([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
                var toDate = to ?? DateTime.UtcNow;

                var revenue = await _paymentService.GetTotalRevenueAsync(fromDate, toDate);
                return Ok(new { totalRevenue = revenue });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving revenue", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê phương thức thanh toán (Admin only)
        /// GET /api/payments/payment-methods-stats?from=2023-01-01&to=2023-12-31
        /// </summary>
        [HttpGet("payment-methods-stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPaymentMethodStats([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
                var toDate = to ?? DateTime.UtcNow;

                var stats = await _paymentService.GetPaymentMethodStatsAsync(fromDate, toDate);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving payment method stats", error = ex.Message });
            }
        }

        // ====== PAYMENT STATUS MANAGEMENT ======

        /// <summary>
        /// Cập nhật trạng thái payment (Admin/Staff only)
        /// PUT /api/payments/{id}/status
        /// </summary>
        [HttpPut("{id:int}/status")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UpdatePaymentStatus(int id, [FromBody] UpdatePaymentStatusRequest request)
        {
            try
            {
                var result = await _paymentService.UpdatePaymentStatusAsync(id, request.Status, request.TransactionId);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating payment status", error = ex.Message });
            }
        }

        public class UpdatePaymentStatusRequest
        {
            public string Status { get; set; } = "";
            public string? TransactionId { get; set; }
        }
    }
}
