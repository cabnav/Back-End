using EVCharging.BE.Services.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller quản lý hóa đơn
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        /// <summary>
        /// Lấy hóa đơn theo InvoiceId
        /// </summary>
        /// <param name="invoiceId">ID hóa đơn</param>
        /// <returns>Thông tin hóa đơn</returns>
        [HttpGet("{invoiceId}")]
        public async Task<IActionResult> GetInvoiceById(int invoiceId)
        {
            try
            {
                // Lấy userId từ JWT token để kiểm tra quyền
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId, currentUserId);

                if (invoice == null)
                {
                    return NotFound(new { message = $"Không tìm thấy hóa đơn với ID: {invoiceId}" });
                }

                return Ok(new { data = invoice });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new 
                { 
                    message = "Đã xảy ra lỗi khi lấy thông tin hóa đơn",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy hóa đơn theo SessionId (để xuất hóa đơn sau khi thanh toán)
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        /// <returns>Thông tin hóa đơn</returns>
        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetInvoiceBySessionId(int sessionId)
        {
            try
            {
                // Lấy userId từ JWT token để kiểm tra quyền
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Gọi service để lấy hóa đơn
                var invoice = await _invoiceService.GetInvoiceBySessionIdAsync(sessionId, currentUserId);

                if (invoice == null)
                {
                    return NotFound(new { message = $"Không tìm thấy hóa đơn cho phiên sạc {sessionId}. Phiên sạc có thể chưa được thanh toán." });
                }

                return Ok(new { data = invoice });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new 
                { 
                    message = "Đã xảy ra lỗi khi lấy thông tin hóa đơn",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy danh sách hóa đơn của người dùng hiện tại
        /// </summary>
        /// <param name="skip">Số bản ghi bỏ qua</param>
        /// <param name="take">Số bản ghi lấy</param>
        /// <returns>Danh sách hóa đơn</returns>
        [HttpGet("my-invoices")]
        public async Task<IActionResult> GetMyInvoices([FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            try
            {
                // Lấy userId từ JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Gọi service để lấy danh sách hóa đơn
                var (items, total) = await _invoiceService.GetUserInvoicesAsync(currentUserId, skip, take);

                return Ok(new
                {
                    total = total,
                    skip = skip,
                    take = take,
                    items = items
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new 
                { 
                    message = "Đã xảy ra lỗi khi lấy danh sách hóa đơn",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy danh sách hóa đơn của một user (có thể dùng cho admin)
        /// </summary>
        /// <param name="userId">ID người dùng</param>
        /// <param name="skip">Số bản ghi bỏ qua</param>
        /// <param name="take">Số bản ghi lấy</param>
        /// <returns>Danh sách hóa đơn</returns>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetInvoicesByUserId(int userId, [FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            try
            {
                // Lấy userId từ JWT token để kiểm tra quyền
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Kiểm tra quyền - chỉ được xem hóa đơn của chính mình
                if (userId != currentUserId)
                {
                    return StatusCode(403, new { message = "Bạn chỉ có thể xem hóa đơn của chính mình." });
                }

                // Gọi service để lấy danh sách hóa đơn
                var (items, total) = await _invoiceService.GetUserInvoicesAsync(userId, skip, take);

                return Ok(new
                {
                    total = total,
                    skip = skip,
                    take = take,
                    items = items
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new 
                { 
                    message = "Đã xảy ra lỗi khi lấy danh sách hóa đơn",
                    error = ex.Message 
                });
            }
        }
    }
}
