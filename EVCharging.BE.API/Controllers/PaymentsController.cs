using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Services.Services.Admin;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
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
        private readonly IInvoiceService _invoiceService;
        private readonly EvchargingManagementContext _db;
        private readonly IDepositService _depositService;

        public PaymentsController(
            IPaymentService paymentService,
            IMomoService momoService,
            IInvoiceService invoiceService,
            EvchargingManagementContext db,
            IDepositService depositService)
        {
            _paymentService = paymentService;
            _momoService = momoService;
            _invoiceService = invoiceService;
            _db = db;
            _depositService = depositService;
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
                // Lưu ý: finalCost = 0 là hợp lệ khi deposit đã cover hết chi phí, vẫn cần thanh toán để tạo invoice và hoàn cọc dư
                if (!session.FinalCost.HasValue)
                {
                    return BadRequest(new { message = "Phiên sạc chưa có chi phí cuối cùng." });
                }

                // Kiểm tra đã thanh toán chưa - kiểm tra cả "success" và "pending" (đang xử lý)
                var existingPayment = await _db.Payments
                    .Where(p => p.SessionId == request.SessionId && 
                               (p.PaymentStatus == "success" || p.PaymentStatus == "pending"))
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (existingPayment != null)
                {
                    if (existingPayment.PaymentStatus == "success")
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
                    else if (existingPayment.PaymentStatus == "pending")
                    {
                        // Payment đang pending - có thể đang xử lý hoặc callback chưa được gọi
                        return BadRequest(new
                        {
                            message = "Đã có giao dịch thanh toán đang được xử lý cho phiên sạc này. Vui lòng đợi hoặc kiểm tra lại sau.",
                            paymentId = existingPayment.PaymentId,
                            paymentStatus = "pending",
                            invoiceNumber = existingPayment.InvoiceNumber
                        });
                    }
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
                    PaymentType = "session_payment",
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
                        // If service returned absolute URL, redirect directly. Otherwise prefix with current host.
                        if (Uri.IsWellFormedUriString(result.RedirectUrl, UriKind.Absolute))
                            return Redirect(result.RedirectUrl);

                        return Redirect($"{Request.Scheme}://{Request.Host}{result.RedirectUrl}");
                    }
                    return BadRequest(new { message = result.ErrorMessage ?? "Payment processing failed" });
                }

                // Redirect to frontend URL returned by service. Support absolute or relative paths.
                if (!string.IsNullOrEmpty(result.RedirectUrl))
                {
                    if (Uri.IsWellFormedUriString(result.RedirectUrl, UriKind.Absolute))
                        return Redirect(result.RedirectUrl);

                    return Redirect($"{Request.Scheme}://{Request.Host}{result.RedirectUrl}");
                }

                return Ok(new { message = "Payment processed" });
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

        /// <summary>
        /// Thanh toán cọc đặt chỗ bằng MoMo (khi ví không đủ)
        /// </summary>
        [HttpPost("reservation-deposit-momo")]
        public async Task<IActionResult> PayReservationDepositByMomo([FromBody] ReservationDepositPaymentRequest request)
        {
            try
            {
                // Kiểm tra validation: chỉ cần ReservationCode
                if (request == null || string.IsNullOrWhiteSpace(request.ReservationCode))
                {
                    return BadRequest(new
                    {
                        message = "ReservationCode là bắt buộc",
                        errors = new[] { "Vui lòng cung cấp ReservationCode" }
                    });
                }

                // Lấy userId từ JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Lấy reservation bằng ReservationCode (người dùng nhận qua email/SMS)
                var driverId = await _db.DriverProfiles
                    .Where(d => d.UserId == currentUserId)
                    .Select(d => d.DriverId)
                    .FirstOrDefaultAsync();
                
                if (driverId == 0)
                {
                    return BadRequest(new { message = "Không tìm thấy hồ sơ tài xế." });
                }
                
                var reservation = await _db.Reservations
                    .Include(r => r.Driver)
                        .ThenInclude(d => d.User)
                    .Include(r => r.Point)
                        .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(r => r.ReservationCode == request.ReservationCode && r.DriverId == driverId);

                if (reservation == null)
                {
                    return BadRequest(new { message = $"Không tìm thấy đặt chỗ với thông tin đã cung cấp." });
                }

                // Kiểm tra quyền sở hữu
                if (reservation.Driver?.UserId != currentUserId)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền thanh toán đặt chỗ này." });
                }

                // Lấy giá tiền cọc hiện tại
                var depositAmount = await _depositService.GetCurrentDepositAmountAsync();

                // Kiểm tra đã thanh toán cọc chưa
                var existingDeposit = await _db.Payments
                    .Where(p => p.ReservationId == reservation.ReservationId && 
                               p.PaymentStatus == "success" &&
                               p.PaymentType == "deposit")
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (existingDeposit != null)
                {
                    return Ok(new
                    {
                        message = "Đặt chỗ đã được thanh toán cọc rồi",
                        alreadyPaid = true,
                        paymentInfo = new PaymentInfoDto
                        {
                            PaymentId = existingDeposit.PaymentId,
                            PaymentMethod = existingDeposit.PaymentMethod ?? "",
                            Amount = existingDeposit.Amount,
                            InvoiceNumber = existingDeposit.InvoiceNumber,
                            PaidAt = existingDeposit.CreatedAt,
                            ReservationId = reservation.ReservationId // ✅ Dùng reservation.ReservationId thay vì request.ReservationId
                        }
                    });
                }

                // Lấy thông tin user
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);
                var fullName = user?.Name ?? "Khách hàng";

                // Tạo Payment record với status "pending"
                // InvoiceNumber tạm thời, sẽ cập nhật sau khi có orderId từ MoMo
                // Lưu ý: InvoiceNumber có max length 50 và unique constraint
                var tempInvoiceNumber = $"RES{reservation.ReservationId}_{DateTime.UtcNow:MMddHHmmss}";
                if (tempInvoiceNumber.Length > 50)
                {
                    tempInvoiceNumber = tempInvoiceNumber.Substring(0, 50);
                }
                
                // Đảm bảo InvoiceNumber không trùng (nếu trùng, thêm random suffix)
                var originalTempInvoice = tempInvoiceNumber;
                int suffix = 0;
                while (await _db.Payments.AnyAsync(p => p.InvoiceNumber == tempInvoiceNumber))
                {
                    suffix++;
                    var suffixStr = suffix.ToString();
                    tempInvoiceNumber = originalTempInvoice.Length + suffixStr.Length <= 50
                        ? originalTempInvoice + suffixStr
                        : originalTempInvoice.Substring(0, 50 - suffixStr.Length) + suffixStr;
                }

                var payment = new PaymentEntity
                {
                    UserId = currentUserId,
                    ReservationId = reservation.ReservationId,
                    Amount = depositAmount,
                    PaymentMethod = "momo",
                    PaymentType = "deposit", // ⭐ Quan trọng: Phân biệt loại payment
                    PaymentStatus = "pending",
                    InvoiceNumber = tempInvoiceNumber, // Tạm thời, sẽ update sau
                    CreatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                
                // ✅ Cập nhật DepositPaymentStatus của reservation
                reservation.DepositPaymentStatus = "pending";
                
                await _db.SaveChangesAsync();

                // Tạo MoMo payment request
                var momoRequest = new MomoCreatePaymentRequestDto
                {
                    SessionId = 0, // Không có session, dùng ReservationId
                    UserId = currentUserId,
                    Amount = depositAmount,
                    FullName = fullName,
                    OrderInfo = $"Cọc đặt chỗ #{reservation.ReservationCode} - Trạm: {reservation.Point?.Station?.Name ?? "N/A"}"
                };

                var momoResponse = await _momoService.CreatePaymentAsync(momoRequest);

                // Cập nhật Payment với orderId từ MoMo
                // Đảm bảo orderId không quá dài (max 50 ký tự) và không trùng
                var finalInvoiceNumber = momoResponse.OrderId ?? payment.InvoiceNumber;
                if (finalInvoiceNumber != null && finalInvoiceNumber.Length > 50)
                {
                    finalInvoiceNumber = finalInvoiceNumber.Substring(0, 50);
                }
                
                // Kiểm tra unique constraint trước khi update
                if (finalInvoiceNumber != null && await _db.Payments.AnyAsync(p => p.InvoiceNumber == finalInvoiceNumber && p.PaymentId != payment.PaymentId))
                {
                    // Nếu trùng, giữ nguyên InvoiceNumber hiện tại
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
                    reservationId = reservation.ReservationId,
                    reservationCode = reservation.ReservationCode,
                    depositAmount = depositAmount
                });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                // Log inner exception để debug constraint violations
                var innerEx = dbEx.InnerException;
                var errorMessage = dbEx.Message;
                if (innerEx != null)
                {
                    errorMessage = $"{errorMessage} | Inner: {innerEx.Message}";
                }
                
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi khi tạo payment record",
                    error = errorMessage,
                    details = "Có thể do constraint violation (unique, foreign key, check constraint). Vui lòng kiểm tra database."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi khi tạo payment URL",
                    error = ex.Message,
                    stackTrace = ex.StackTrace // Thêm stack trace để debug
                });
            }
        }

        /// <summary>
        /// Lấy danh sách sessions chưa thanh toán của user (để user check và bấm thanh toán)
        /// </summary>
        /// <param name="skip">Số bản ghi bỏ qua</param>
        /// <param name="take">Số bản ghi lấy</param>
        /// <returns>Danh sách sessions chưa thanh toán với đầy đủ thông tin ChargingSession</returns>
        [HttpGet("unpaid-sessions")]
        public async Task<IActionResult> GetUnpaidSessions([FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            try
            {
                // Lấy userId từ JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Gọi service để lấy unpaid sessions
                var result = await _paymentService.GetUnpaidSessionsAsync(currentUserId, skip, take);

                return Ok(new
                {
                    message = "Lấy danh sách sessions chưa thanh toán thành công",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi khi lấy danh sách sessions chưa thanh toán",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy danh sách invoices đã thanh toán của user
        /// </summary>
        /// <param name="skip">Số bản ghi bỏ qua</param>
        /// <param name="take">Số bản ghi lấy</param>
        /// <returns>Danh sách invoices đã thanh toán với đầy đủ thông tin ChargingSession</returns>
        [HttpGet("paid-invoices")]
        public async Task<IActionResult> GetPaidInvoices([FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            try
            {
                // Lấy userId từ JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
                }

                // Gọi service để lấy paid invoices
                var result = await _paymentService.GetPaidInvoicesAsync(currentUserId, skip, take);

                return Ok(new
                {
                    message = "Lấy danh sách invoices đã thanh toán thành công",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi khi lấy danh sách invoices đã thanh toán",
                    error = ex.Message
                });
            }
        }
    }

    /// <summary>
    /// Request để thanh toán cọc đặt chỗ
    /// Chỉ nhận ReservationCode (string) - người dùng nhận qua email/SMS
    /// </summary>
    public class ReservationDepositPaymentRequest
    {
        /// <summary>
        /// ReservationCode (string) - Bắt buộc: Mã đặt chỗ người dùng nhận qua email/SMS
        /// Ví dụ: "T83JU4CP"
        /// </summary>
        [Required(ErrorMessage = "ReservationCode là bắt buộc")]
        public string ReservationCode { get; set; } = string.Empty;
    }
}

