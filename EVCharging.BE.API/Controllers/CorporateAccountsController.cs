using EVCharging.BE.Common.DTOs.Corporates;
using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Services.Services.Users;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/corporate")]
    [Authorize]
    public class CorporateAccountsController : ControllerBase
    {
        private readonly ICorporateAccountService _svc;
        private readonly IMomoService _momoService;
        private readonly EvchargingManagementContext _db;

        public CorporateAccountsController(
            ICorporateAccountService svc,
            IMomoService momoService,
            EvchargingManagementContext db)
        {
            _svc = svc;
            _momoService = momoService;
            _db = db;
        }

        /// <summary>Create a corporate account</summary>
        [HttpPost(Name = "Corporate_Create")]
        public async Task<IActionResult> CreateCorporateAccount([FromBody] CorporateAccountCreateRequest req)
        {
            try
            {
                var dto = await _svc.CreateAsync(req);

                // ✅ Tránh lỗi "No route matches" – trả Location thủ công (đơn giản, an toàn)
                return Created($"/api/corporate/{dto.CorporateId}", dto);
                // Hoặc nếu sau này có action GetById chuẩn:
                // return CreatedAtAction(nameof(GetCorporateAccountById), new { corporateId = dto.CorporateId }, dto);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>List corporate accounts (paging + search)</summary>
        [HttpGet(Name = "Corporate_List")] // ✅ bỏ template thừa & khoảng trắng
        public async Task<IActionResult> GetCorporateAccounts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? q = null)
            => Ok(await _svc.GetAllAsync(page, pageSize, q));

        /// <summary>Get one corporate by id</summary>
        [HttpGet("{corporateId:int}", Name = "Corporate_GetById")]
        public async Task<IActionResult> GetCorporateAccountById([FromRoute] int corporateId)
        {
            try
            {
                var dto = await _svc.GetByIdAsync(corporateId);
                return dto == null ? NotFound(new { message = "Corporate not found" }) : Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>Get pending drivers of corporate</summary>
        [HttpGet("{corporateId:int}/drivers/pending", Name = "Corporate_GetPendingDrivers")]
        public async Task<IActionResult> GetPendingDrivers([FromRoute] int corporateId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var drivers = await _svc.GetPendingDriversAsync(corporateId, currentUserId);
                return Ok(drivers);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Get drivers of corporate by status</summary>
        [HttpGet("{corporateId:int}/drivers", Name = "Corporate_GetDrivers")]
        public async Task<IActionResult> GetDrivers(
            [FromRoute] int corporateId,
            [FromQuery] string? status = "active")
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var drivers = await _svc.GetDriversAsync(corporateId, currentUserId, status);
                return Ok(drivers);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Approve driver to corporate</summary>
        [HttpPost("{corporateId:int}/drivers/{driverId:int}/approve", Name = "Corporate_ApproveDriver")]
        public async Task<IActionResult> ApproveDriver(
            [FromRoute] int corporateId,
            [FromRoute] int driverId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var result = await _svc.ApproveDriverAsync(corporateId, driverId, currentUserId);
                return Ok(new { success = true, message = "Driver đã được chấp nhận" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Reject driver from corporate</summary>
        [HttpPost("{corporateId:int}/drivers/{driverId:int}/reject", Name = "Corporate_RejectDriver")]
        public async Task<IActionResult> RejectDriver(
            [FromRoute] int corporateId,
            [FromRoute] int driverId,
            [FromBody] RejectDriverRequest? request = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var reason = request?.Reason;
                var result = await _svc.RejectDriverAsync(corporateId, driverId, currentUserId, reason);
                return Ok(new { success = true, message = "Driver đã bị từ chối" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        // ====== INVOICE MANAGEMENT (POSTPAID) ======

        /// <summary>Get pending sessions (chưa có invoice) of corporate</summary>
        [HttpGet("{corporateId:int}/sessions/pending", Name = "Corporate_GetPendingSessions")]
        public async Task<IActionResult> GetPendingSessions([FromRoute] int corporateId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var sessions = await _svc.GetPendingSessionsAsync(corporateId, currentUserId);
                return Ok(sessions);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Generate invoice for corporate (gom sessions pending)</summary>
        [HttpPost("{corporateId:int}/invoices/generate", Name = "Corporate_GenerateInvoice")]
        public async Task<IActionResult> GenerateInvoice(
            [FromRoute] int corporateId,
            [FromBody] GenerateCorporateInvoiceRequest? request = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var invoice = await _svc.GenerateInvoiceAsync(corporateId, currentUserId, request);
                return Ok(invoice);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Get list of corporate invoices</summary>
        [HttpGet("{corporateId:int}/invoices", Name = "Corporate_GetInvoices")]
        public async Task<IActionResult> GetCorporateInvoices(
            [FromRoute] int corporateId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var (items, total) = await _svc.GetCorporateInvoicesAsync(corporateId, currentUserId, skip, take);
                return Ok(new { total, skip, take, items });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Get corporate invoice by ID</summary>
        [HttpGet("{corporateId:int}/invoices/{invoiceId:int}", Name = "Corporate_GetInvoiceById")]
        public async Task<IActionResult> GetCorporateInvoiceById(
            [FromRoute] int corporateId,
            [FromRoute] int invoiceId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var invoice = await _svc.GetCorporateInvoiceByIdAsync(corporateId, invoiceId, currentUserId);
                if (invoice == null)
                    return NotFound(new { message = "Invoice không tồn tại" });

                return Ok(invoice);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Pay corporate invoice</summary>
        [HttpPost("{corporateId:int}/invoices/{invoiceId:int}/pay", Name = "Corporate_PayInvoice")]
        public async Task<IActionResult> PayCorporateInvoice(
            [FromRoute] int corporateId,
            [FromRoute] int invoiceId,
            [FromBody] PayCorporateInvoiceRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // ✅ Kiểm tra quyền
                var corporate = await _db.CorporateAccounts
                    .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
                
                if (corporate == null)
                    return NotFound(new { message = "Corporate không tồn tại" });
                
                if (corporate.AdminUserId != currentUserId)
                    return Unauthorized(new { message = "Bạn không có quyền thanh toán invoice này" });

                // ✅ Lấy invoice
                var invoice = await _db.Invoices
                    .Include(i => i.Corporate)
                    .Include(i => i.InvoiceItems)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.CorporateId == corporateId);

                if (invoice == null)
                    return NotFound(new { message = "Invoice không tồn tại" });

                if (invoice.Status == "paid")
                    return BadRequest(new { message = "Invoice đã được thanh toán rồi" });

                // ✅ Validate amount
                if (invoice.TotalAmount <= 0)
                {
                    return BadRequest(new 
                    { 
                        message = "Số tiền hóa đơn không hợp lệ (phải lớn hơn 0)",
                        invoiceId = invoice.InvoiceId,
                        totalAmount = invoice.TotalAmount,
                        itemCount = invoice.InvoiceItems?.Count ?? 0
                    });
                }

                // Momo yêu cầu amount >= 1000 VND
                if (invoice.TotalAmount < 1000)
                    return BadRequest(new { message = "Số tiền thanh toán tối thiểu là 1,000 VND. Số tiền hiện tại: " + invoice.TotalAmount.ToString("N0") + " VND" });

                // ✅ Nếu PaymentMethod = "momo", tạo Momo payment URL
                if (request.PaymentMethod?.ToLower() == "momo")
                {
                    // Lấy thông tin user
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);
                    var fullName = user?.Name ?? "Khách hàng";

                    // Làm tròn amount về số nguyên (VND không có decimal)
                    var amount = Math.Round(invoice.TotalAmount, 0, MidpointRounding.AwayFromZero);

                    // Tạo Payment record với status "pending"
                    var orderId = $"CORP-INV-{invoiceId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{currentUserId}";
                    if (orderId.Length > 50)
                    {
                        orderId = orderId.Substring(0, 50);
                    }

                    // Kiểm tra InvoiceNumber không trùng
                    var originalOrderId = orderId;
                    int suffix = 0;
                    while (await _db.Payments.AnyAsync(p => p.InvoiceNumber == orderId))
                    {
                        suffix++;
                        var suffixStr = suffix.ToString();
                        orderId = originalOrderId.Length + suffixStr.Length <= 50
                            ? originalOrderId + suffixStr
                            : originalOrderId.Substring(0, 50 - suffixStr.Length) + suffixStr;
                    }

                    var payment = new PaymentEntity
                    {
                        UserId = currentUserId,
                        SessionId = null, // Corporate invoice không có SessionId cụ thể
                        Amount = amount,
                        PaymentMethod = "momo",
                        PaymentStatus = "pending",
                        PaymentType = "corporate_invoice",
                        InvoiceNumber = orderId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.Payments.Add(payment);
                    await _db.SaveChangesAsync();

                    // Tạo MoMo payment request
                    var momoRequest = new MomoCreatePaymentRequestDto
                    {
                        SessionId = 0, // Không có session, dùng InvoiceId
                        UserId = currentUserId,
                        Amount = amount,
                        FullName = fullName,
                        OrderInfo = $"Thanh toán hóa đơn #{invoice.InvoiceNumber} - {corporate.CompanyName}",
                        InvoiceId = invoiceId // Cho Corporate Invoice
                    };

                    var momoResponse = await _momoService.CreatePaymentAsync(momoRequest);

                    // Cập nhật Payment với orderId từ MoMo
                    var finalInvoiceNumber = momoResponse.OrderId ?? payment.InvoiceNumber;
                    if (finalInvoiceNumber != null && finalInvoiceNumber.Length > 50)
                    {
                        finalInvoiceNumber = finalInvoiceNumber.Substring(0, 50);
                    }

                    // Kiểm tra unique constraint
                    if (finalInvoiceNumber != null && await _db.Payments.AnyAsync(p => p.InvoiceNumber == finalInvoiceNumber && p.PaymentId != payment.PaymentId))
                    {
                        finalInvoiceNumber = payment.InvoiceNumber;
                    }

                    if (finalInvoiceNumber != null)
                    {
                        payment.InvoiceNumber = finalInvoiceNumber;
                        await _db.SaveChangesAsync();
                    }

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
                        paymentId = payment.PaymentId,
                        invoiceId = invoice.InvoiceId,
                        invoiceNumber = invoice.InvoiceNumber
                    });
                }

                // ✅ PaymentMethod khác (bank_transfer, cash, wallet) - Thanh toán trực tiếp
                var result = await _svc.PayCorporateInvoiceAsync(corporateId, invoiceId, currentUserId, request);
                return Ok(new { success = true, message = "Invoice đã được thanh toán thành công" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }
    }

    public class RejectDriverRequest
    {
        public string? Reason { get; set; }
    }
}