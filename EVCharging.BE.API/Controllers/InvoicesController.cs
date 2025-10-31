using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly EvchargingManagementContext _db;

        public InvoicesController(EvchargingManagementContext db)
        {
            _db = db;
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

                var invoice = await _db.Invoices
                    .Include(i => i.InvoiceItems)
                        .ThenInclude(item => item.Session!)
                            .ThenInclude(s => s.Point)
                                .ThenInclude(p => p.Station)
                    .Include(i => i.User)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

                if (invoice == null)
                {
                    return NotFound(new { message = $"Không tìm thấy hóa đơn với ID: {invoiceId}" });
                }

                // Kiểm tra quyền - chỉ chủ sở hữu mới được xem hóa đơn
                if (invoice.UserId != currentUserId)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền xem hóa đơn này." });
                }

                var invoiceResponse = await MapToInvoiceResponseDtoAsync(invoice);
                return Ok(new { data = invoiceResponse });
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

                // Kiểm tra session có tồn tại và thuộc về user không
                var session = await _db.ChargingSessions
                    .Include(s => s.Driver)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                {
                    return NotFound(new { message = $"Không tìm thấy phiên sạc với ID: {sessionId}" });
                }

                if (session.Driver?.UserId != currentUserId)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền xem hóa đơn của phiên sạc này." });
                }

                // Lấy payment của session này để lấy invoice number
                var payment = await _db.Payments
                    .Where(p => p.SessionId == sessionId && p.PaymentStatus == "success")
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (payment == null || string.IsNullOrEmpty(payment.InvoiceNumber))
                {
                    return NotFound(new { message = $"Không tìm thấy hóa đơn cho phiên sạc {sessionId}. Phiên sạc này chưa được thanh toán." });
                }

                // Lấy invoice theo invoice number
                var invoice = await _db.Invoices
                    .Include(i => i.InvoiceItems)
                        .ThenInclude(item => item.Session!)
                            .ThenInclude(s => s.Point)
                                .ThenInclude(p => p.Station)
                    .Include(i => i.User)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == payment.InvoiceNumber);

                if (invoice == null)
                {
                    return NotFound(new { message = $"Không tìm thấy hóa đơn với số: {payment.InvoiceNumber}" });
                }

                var invoiceResponse = await MapToInvoiceResponseDtoAsync(invoice);
                return Ok(new { data = invoiceResponse });
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

                var invoices = await _db.Invoices
                    .Include(i => i.InvoiceItems)
                        .ThenInclude(item => item.Session!)
                            .ThenInclude(s => s.Point)
                                .ThenInclude(p => p.Station)
                    .Where(i => i.UserId == currentUserId)
                    .OrderByDescending(i => i.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                var total = await _db.Invoices
                    .Where(i => i.UserId == currentUserId)
                    .CountAsync();

                var invoiceList = new List<InvoiceResponseDto>();
                foreach (var invoice in invoices)
                {
                    invoiceList.Add(await MapToInvoiceResponseDtoAsync(invoice));
                }

                return Ok(new
                {
                    total = total,
                    skip = skip,
                    take = take,
                    items = invoiceList
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

                var invoices = await _db.Invoices
                    .Include(i => i.InvoiceItems)
                        .ThenInclude(item => item.Session!)
                            .ThenInclude(s => s.Point)
                                .ThenInclude(p => p.Station)
                    .Where(i => i.UserId == userId)
                    .OrderByDescending(i => i.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                var total = await _db.Invoices
                    .Where(i => i.UserId == userId)
                    .CountAsync();

                var invoiceList = new List<InvoiceResponseDto>();
                foreach (var invoice in invoices)
                {
                    invoiceList.Add(await MapToInvoiceResponseDtoAsync(invoice));
                }

                return Ok(new
                {
                    total = total,
                    skip = skip,
                    take = take,
                    items = invoiceList
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
        /// Map Invoice entity sang InvoiceResponseDto
        /// </summary>
        private async Task<InvoiceResponseDto> MapToInvoiceResponseDtoAsync(Invoice invoice)
        {
            var firstItem = invoice.InvoiceItems.FirstOrDefault();
            var session = firstItem?.Session;

            // Lấy phương thức thanh toán từ bảng Payments
            var payment = await _db.Payments
                .Where(p => p.InvoiceNumber == invoice.InvoiceNumber && p.PaymentStatus == "success")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            return new InvoiceResponseDto
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                UserId = invoice.UserId,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status,
                PaymentMethod = payment?.PaymentMethod, // wallet hoặc cash
                CreatedAt = invoice.CreatedAt,
                PaidAt = invoice.PaidAt,
                Items = invoice.InvoiceItems.Select(item => new InvoiceItemDto
                {
                    ItemId = item.ItemId,
                    SessionId = item.SessionId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount
                }).ToList(),
                SessionInfo = session != null ? new SessionInfoDto
                {
                    SessionId = session.SessionId,
                    StationName = session.Point?.Station?.Name,
                    StationAddress = session.Point?.Station?.Address,
                    EnergyUsed = session.EnergyUsed,
                    DurationMinutes = session.DurationMinutes,
                    CostBeforeDiscount = session.CostBeforeDiscount,
                    AppliedDiscount = session.AppliedDiscount,
                    FinalCost = session.FinalCost,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime
                } : null
            };
        }
    }
}
