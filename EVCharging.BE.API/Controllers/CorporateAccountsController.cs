using EVCharging.BE.Common.DTOs.Corporates;
using EVCharging.BE.Services.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/corporate")]
    [Authorize]
    public class CorporateAccountsController : ControllerBase
    {
        private readonly ICorporateAccountService _svc;

        public CorporateAccountsController(ICorporateAccountService svc)
        {
            _svc = svc;
        }

        /// <summary>
        /// Helper method: Lấy CorporateId từ AdminUserId của user hiện tại
        /// </summary>
        private async Task<int> GetCorporateIdFromCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
            {
                throw new UnauthorizedAccessException("Không thể xác định người dùng. Vui lòng đăng nhập lại.");
            }

            var corporate = await _svc.GetByAdminUserIdAsync(currentUserId);
            if (corporate == null)
            {
                throw new KeyNotFoundException("Bạn chưa có corporate account. Vui lòng tạo corporate account trước.");
            }

            return corporate.CorporateId;
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

        /// <summary>Get my corporate account (tự động lấy từ AdminUserId)</summary>
        [HttpGet("my", Name = "Corporate_GetMy")]
        public async Task<IActionResult> GetMyCorporateAccount()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var dto = await _svc.GetByAdminUserIdAsync(currentUserId);
                if (dto == null)
                    return NotFound(new { message = "Bạn chưa có corporate account. Vui lòng tạo corporate account trước." });

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>Get pending drivers of corporate (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpGet("drivers/pending", Name = "Corporate_GetPendingDrivers")]
        public async Task<IActionResult> GetPendingDrivers()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var corporateId = await GetCorporateIdFromCurrentUserAsync();
                var drivers = await _svc.GetPendingDriversAsync(corporateId, currentUserId);
                var driversList = drivers.ToList();
                
                if (!driversList.Any())
                    return NotFound(new { message = "Không tìm thấy tài xế đang chờ duyệt" });

                return Ok(driversList);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Get drivers of corporate by status (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpGet("drivers", Name = "Corporate_GetDrivers")]
        public async Task<IActionResult> GetDrivers([FromQuery] string? status = "active")
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var corporateId = await GetCorporateIdFromCurrentUserAsync();
                var drivers = await _svc.GetDriversAsync(corporateId, currentUserId, status);
                var driversList = drivers.ToList();
                
                if (!driversList.Any())
                    return NotFound(new { message = $"Không tìm thấy tài xế với trạng thái '{status}'" });

                return Ok(driversList);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Approve driver to corporate (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpPost("drivers/{driverId:int}/approve", Name = "Corporate_ApproveDriver")]
        public async Task<IActionResult> ApproveDriver([FromRoute] int driverId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var corporateId = await GetCorporateIdFromCurrentUserAsync();
                var result = await _svc.ApproveDriverAsync(corporateId, driverId, currentUserId);
                return Ok(new { success = true, message = "Driver đã được chấp nhận" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Reject driver from corporate (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpPost("drivers/{driverId:int}/reject", Name = "Corporate_RejectDriver")]
        public async Task<IActionResult> RejectDriver(
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

                var corporateId = await GetCorporateIdFromCurrentUserAsync();
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

        /// <summary>Get pending sessions (chưa có invoice) of corporate (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpGet("sessions/pending", Name = "Corporate_GetPendingSessions")]
        public async Task<IActionResult> GetPendingSessions()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var corporateId = await GetCorporateIdFromCurrentUserAsync();
                var sessions = await _svc.GetPendingSessionsAsync(corporateId, currentUserId);
                var sessionsList = sessions.ToList();
                
                if (!sessionsList.Any())
                    return NotFound(new { message = "Không tìm thấy phiên sạc đang chờ tạo hóa đơn" });

                return Ok(sessionsList);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Generate invoice for corporate (gom sessions pending) (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpPost("invoices/generate", Name = "Corporate_GenerateInvoice")]
        public async Task<IActionResult> GenerateInvoice([FromBody] GenerateCorporateInvoiceRequest? request = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var corporateId = await GetCorporateIdFromCurrentUserAsync();
                var invoice = await _svc.GenerateInvoiceAsync(corporateId, currentUserId, request);
                return Ok(invoice);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Get list of corporate invoices (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpGet("invoices", Name = "Corporate_GetInvoices")]
        public async Task<IActionResult> GetCorporateInvoices(
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

                var corporateId = await GetCorporateIdFromCurrentUserAsync();
                var (items, total) = await _svc.GetCorporateInvoicesAsync(corporateId, currentUserId, skip, take);
                var itemsList = items.ToList();
                
                if (total == 0 || !itemsList.Any())
                    return NotFound(new { message = "Không tìm thấy hóa đơn nào" });

                return Ok(new { total, skip, take, items = itemsList });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Get corporate invoice by ID (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpGet("invoices/{invoiceId:int}", Name = "Corporate_GetInvoiceById")]
        public async Task<IActionResult> GetCorporateInvoiceById([FromRoute] int invoiceId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                var corporateId = await GetCorporateIdFromCurrentUserAsync();
                var invoice = await _svc.GetCorporateInvoiceByIdAsync(corporateId, invoiceId, currentUserId);
                if (invoice == null)
                    return NotFound(new { message = "Invoice không tồn tại" });

                return Ok(invoice);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>Pay corporate invoice (tự động lấy corporateId từ AdminUserId)</summary>
        [HttpPost("invoices/{invoiceId:int}/pay", Name = "Corporate_PayInvoice")]
        public async Task<IActionResult> PayCorporateInvoice(
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

                var corporateId = await GetCorporateIdFromCurrentUserAsync();

                // ✅ Nếu PaymentMethod = "momo", gọi service để tạo Momo payment URL
                if (request.PaymentMethod?.ToLower() == "momo")
                {
                    var result = await _svc.PayCorporateInvoiceWithMomoAsync(corporateId, invoiceId, currentUserId);
                    return Ok(result);
                }

                // ✅ PaymentMethod khác (bank_transfer, cash, wallet) - Thanh toán trực tiếp
                var paymentResult = await _svc.PayCorporateInvoiceAsync(corporateId, invoiceId, currentUserId, request);
                return Ok(new { success = true, message = "Invoice đã được thanh toán thành công" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }
    }

    public class RejectDriverRequest
    {
        public string? Reason { get; set; }
    }
}