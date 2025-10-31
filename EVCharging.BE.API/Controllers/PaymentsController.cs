using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
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
    }
}

