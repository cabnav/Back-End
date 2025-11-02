using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller quản lý thanh toán cho phiên sạc
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IMomoService _momoService;
        private readonly EvchargingManagementContext _db;

        public PaymentsController(
            IPaymentService paymentService,
            IMomoService momoService,
            EvchargingManagementContext db)
        {
            _paymentService = paymentService;
            _momoService = momoService;
            _db = db;
        }

        /// <summary>
        /// Thanh toán phiên sạc bằng SessionId - Trừ tiền trực tiếp từ ví người dùng
        /// Chỉ người dùng sở hữu phiên sạc mới được thanh toán
        /// </summary>
        /// <param name="request">Thông tin SessionId cần thanh toán</param>
        /// <returns>Kết quả thanh toán</returns>
        [HttpPost("pay-by-session")]
        public async Task<IActionResult> PayBySession([FromBody] SessionPaymentRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Invalid request data",
                        errors = ModelState.Values.SelectMany(v => v.Errors)
                    });
                }

                // Lấy userId từ JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Gọi service để xử lý thanh toán
                var result = await _paymentService.PayByWalletAsync(request.SessionId, currentUserId);

                if (result.AlreadyPaid)
                {
                    return Ok(new
                    {
                        message = result.Message,
                        alreadyPaid = true,
                        paymentInfo = result.ExistingPaymentInfo
                    });
                }

                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }

                return Ok(new
                {
                    message = result.Message,
                    success = result.Success,
                    paymentInfo = result.PaymentInfo,
                    walletInfo = result.WalletInfo,
                    invoice = result.Invoice
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message,
                    error = "Payment processing failed"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi khi xử lý thanh toán",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Thanh toán tiền mặt cho phiên sạc - Tạo hóa đơn thanh toán thành công
        /// Chỉ người dùng sở hữu phiên sạc mới được thanh toán
        /// </summary>
        /// <param name="request">Thông tin SessionId cần thanh toán</param>
        /// <returns>Hóa đơn thanh toán thành công</returns>
        [HttpPost("pay-by-session-cash")]
        public async Task<IActionResult> PayBySessionCash([FromBody] SessionPaymentRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Invalid request data",
                        errors = ModelState.Values.SelectMany(v => v.Errors)
                    });
                }

                // Lấy userId từ JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Gọi service để xử lý thanh toán tiền mặt
                var result = await _paymentService.PayByCashAsync(request.SessionId, currentUserId);

                if (result.AlreadyPaid)
                {
                    return Ok(new
                    {
                        message = result.Message,
                        alreadyPaid = true,
                        paymentInfo = result.ExistingPaymentInfo
                    });
                }

                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }

                return Ok(new
                {
                    message = result.Message,
                    success = result.Success,
                    paymentInfo = result.PaymentInfo,
                    invoice = result.Invoice
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message,
                    error = "Payment processing failed"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi khi xử lý thanh toán tiền mặt",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Tạo payment URL từ MoMo cho phiên sạc
        /// </summary>
        /// <param name="request">Thông tin SessionId cần thanh toán</param>
        /// <returns>Payment URL từ MoMo</returns>
        [HttpPost("pay-by-session-momo")]
        public async Task<IActionResult> PayBySessionMomo([FromBody] SessionPaymentRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Invalid request data",
                        errors = ModelState.Values.SelectMany(v => v.Errors)
                    });
                }

                // Lấy userId từ JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Lấy thông tin phiên sạc
                var session = await _db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

                if (session == null)
                {
                    return BadRequest(new { message = $"Không tìm thấy phiên sạc với SessionId: {request.SessionId}" });
                }

                // Kiểm tra quyền sở hữu
                if (session.Driver?.UserId != currentUserId)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền thanh toán phiên sạc này." });
                }

                // Kiểm tra phiên sạc đã hoàn thành chưa
                if (session.Status != "completed")
                {
                    return BadRequest(new { message = $"Phiên sạc chưa hoàn thành. Trạng thái hiện tại: {session.Status}" });
                }

                // Kiểm tra đã có chi phí cuối cùng chưa
                if (!session.FinalCost.HasValue || session.FinalCost.Value <= 0)
                {
                    return BadRequest(new { message = "Phiên sạc chưa có chi phí cuối cùng." });
                }

                // Kiểm tra đã thanh toán chưa
                var existingPayment = await _db.Payments
                    .Where(p => p.SessionId == request.SessionId && p.PaymentStatus == "success")
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (existingPayment != null)
                {
                    return Ok(new
                    {
                        message = "Phiên sạc đã được thanh toán rồi",
                        alreadyPaid = true,
                        paymentInfo = new PaymentInfoDto
                        {
                            PaymentId = existingPayment.PaymentId,
                            PaymentMethod = existingPayment.PaymentMethod ?? "",
                            Amount = existingPayment.Amount,
                            InvoiceNumber = existingPayment.InvoiceNumber,
                            PaidAt = existingPayment.CreatedAt,
                            SessionId = request.SessionId
                        }
                    });
                }

                // Lấy thông tin user
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);
                var fullName = user?.Name ?? "Khách hàng";

                // Tạo Payment record với status "pending"
                var orderId = $"{request.SessionId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{currentUserId}";
                var payment = new PaymentEntity
                {
                    UserId = currentUserId,
                    SessionId = request.SessionId,
                    Amount = session.FinalCost.Value,
                    PaymentMethod = "momo",
                    PaymentStatus = "pending",
                    InvoiceNumber = orderId, // Tạm thời dùng orderId làm InvoiceNumber, sẽ cập nhật sau
                    CreatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                // Tạo MoMo payment request
                var momoRequest = new MomoCreatePaymentRequestDto
                {
                    SessionId = request.SessionId,
                    UserId = currentUserId,
                    Amount = session.FinalCost.Value,
                    FullName = fullName,
                    OrderInfo = $"Thanh toán phiên sạc #{request.SessionId} - Trạm: {session.Point.Station.Name}"
                };

                var momoResponse = await _momoService.CreatePaymentAsync(momoRequest);

                // Cập nhật Payment với orderId từ MoMo
                payment.InvoiceNumber = momoResponse.OrderId;
                await _db.SaveChangesAsync();

                if (momoResponse.ErrorCode != 0)
                {
                    // Nếu có lỗi, xóa payment record và trả về lỗi
                    _db.Payments.Remove(payment);
                    await _db.SaveChangesAsync();

                    return BadRequest(new
                    {
                        message = $"Lỗi khi tạo payment: {momoResponse.Message}",
                        errorCode = momoResponse.ErrorCode
                    });
                }

                return Ok(new
                {
                    message = "Tạo payment URL thành công",
                    payUrl = momoResponse.PayUrl,
                    orderId = momoResponse.OrderId,
                    qrCodeUrl = momoResponse.QrCodeUrl,
                    deeplink = momoResponse.Deeplink,
                    paymentId = payment.PaymentId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi khi tạo payment URL",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Xử lý callback từ MoMo sau khi thanh toán (Return URL)
        /// </summary>
        [HttpGet("momo-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> MomoCallback()
        {
            try
            {
                var result = await _momoService.ProcessCallbackAsync(Request.Query);

                if (!result.Success)
                {
                    if (!string.IsNullOrEmpty(result.RedirectUrl))
                    {
                        return Redirect($"{Request.Scheme}://{Request.Host}{result.RedirectUrl}");
                    }
                    return BadRequest(new { message = result.ErrorMessage ?? "Payment processing failed" });
                }

                // Redirect đến trang thành công hoặc thất bại
                return Redirect($"{Request.Scheme}://{Request.Host}{result.RedirectUrl}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error processing callback", error = ex.Message });
            }
        }

        /// <summary>
        /// Xử lý notify từ MoMo sau khi thanh toán (Notify URL - IPN)
        /// </summary>
        [HttpPost("momo-notify")]
        [AllowAnonymous]
        public async Task<IActionResult> MomoNotify()
        {
            try
            {
                var result = await _momoService.ProcessNotifyAsync(Request.Query);

                // MoMo yêu cầu trả về JSON với format này
                return Ok(new
                {
                    message = result.Message,
                    orderId = result.OrderId,
                    status = result.PaymentStatus
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error processing notify", error = ex.Message });
            }
        }
    }
}

