using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly IWalletService _walletService;
        private readonly EvchargingManagementContext _db;

        public PaymentsController(
            IWalletService walletService,
            EvchargingManagementContext db)
        {
            _walletService = walletService;
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

                // Lấy thông tin phiên sạc
                var session = await _db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

                if (session == null)
                {
                    return NotFound(new { message = $"Không tìm thấy phiên sạc với SessionId: {request.SessionId}" });
                }

                // Kiểm tra người dùng có phải chủ sở hữu phiên sạc không
                if (session.Driver == null || session.Driver.UserId != currentUserId)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền thanh toán phiên sạc này. Phiên sạc này thuộc về người dùng khác." });
                }

                // Kiểm tra phiên sạc đã hoàn thành chưa
                if (session.Status != "completed")
                {
                    return BadRequest(new 
                    { 
                        message = $"Phiên sạc chưa hoàn thành. Trạng thái hiện tại: {session.Status}. Vui lòng đợi phiên sạc hoàn thành trước khi thanh toán." 
                    });
                }

                // Kiểm tra đã có chi phí cuối cùng chưa
                if (!session.FinalCost.HasValue || session.FinalCost.Value <= 0)
                {
                    return BadRequest(new 
                    { 
                        message = "Phiên sạc chưa có chi phí cuối cùng. Vui lòng đợi hệ thống tính toán chi phí." 
                    });
                }

                // Kiểm tra đã thanh toán chưa (bất kỳ phương thức nào - tránh thanh toán lại)
                var alreadyPaid = await _db.Payments
                    .AnyAsync(p => p.SessionId == session.SessionId && 
                                  p.PaymentStatus == "success");

                if (alreadyPaid)
                {
                    // Lấy thông tin payment đã thanh toán (bất kỳ phương thức nào)
                    var existingPayment = await _db.Payments
                        .Where(p => p.SessionId == session.SessionId && 
                                   p.PaymentStatus == "success")
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();

                    // Nếu đã thanh toán bằng ví, lấy thông tin transaction
                    WalletTransaction? existingTransaction = null;
                    if (existingPayment?.PaymentMethod == "wallet")
                    {
                        existingTransaction = await _db.WalletTransactions
                            .Where(wt => wt.UserId == session.Driver.UserId && 
                                        wt.ReferenceId == session.SessionId && 
                                        wt.TransactionType == "debit")
                            .OrderByDescending(wt => wt.CreatedAt)
                            .FirstOrDefaultAsync();
                    }

                    return Ok(new 
                    { 
                        message = "Phiên sạc đã được thanh toán rồi",
                        alreadyPaid = true,
                        paymentInfo = new
                        {
                            paymentId = existingPayment?.PaymentId,
                            paymentMethod = existingPayment?.PaymentMethod,
                            amount = existingPayment?.Amount,
                            invoiceNumber = existingPayment?.InvoiceNumber,
                            paidAt = existingPayment?.CreatedAt,
                            sessionId = session.SessionId,
                            transactionId = existingTransaction?.TransactionId
                        }
                    });
                }

                // Kiểm tra số dư ví có đủ không
                var currentBalance = await _walletService.GetBalanceAsync(session.Driver.UserId);
                if (currentBalance < session.FinalCost.Value)
                {
                    return BadRequest(new 
                    { 
                        message = $"Số dư ví không đủ. Số dư hiện tại: {currentBalance:F0} VND, Cần: {session.FinalCost.Value:F0} VND",
                        currentBalance = currentBalance,
                        requiredAmount = session.FinalCost.Value,
                        insufficientAmount = session.FinalCost.Value - currentBalance
                    });
                }

                // Thực hiện trừ tiền từ ví
                var paymentDescription = $"Thanh toán phiên sạc #{session.SessionId} - Trạm: {session.Point.Station.Name}";
                await _walletService.DebitAsync(
                    session.Driver.UserId,
                    session.FinalCost.Value,
                    paymentDescription,
                    session.SessionId
                );

                // Lấy số dư mới sau khi trừ
                var newBalance = await _walletService.GetBalanceAsync(session.Driver.UserId);

                // Tạo số hóa đơn
                var invoiceNumber = $"INV-WALLET-{session.SessionId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);

                // Tạo Invoice
                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    UserId = session.Driver.UserId,
                    BillingPeriodStart = currentDate,
                    BillingPeriodEnd = currentDate,
                    TotalAmount = session.FinalCost.Value,
                    Status = "paid",
                    DueDate = currentDate,
                    CreatedAt = DateTime.UtcNow,
                    PaidAt = DateTime.UtcNow
                };

                _db.Invoices.Add(invoice);
                await _db.SaveChangesAsync();

                // Tạo InvoiceItem
                var invoiceItem = new InvoiceItem
                {
                    InvoiceId = invoice.InvoiceId,
                    SessionId = session.SessionId,
                    Description = $"Phiên sạc #{session.SessionId} - Trạm: {session.Point.Station.Name}",
                    Quantity = 1,
                    UnitPrice = session.FinalCost.Value,
                    Amount = session.FinalCost.Value
                };

                _db.InvoiceItems.Add(invoiceItem);
                await _db.SaveChangesAsync();

                // Tạo bản ghi Payment
                var payment = new Payment
                {
                    UserId = session.Driver.UserId,
                    SessionId = session.SessionId,
                    Amount = session.FinalCost.Value,
                    PaymentMethod = "wallet",
                    PaymentStatus = "success",
                    InvoiceNumber = invoiceNumber,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                // Lấy thông tin giao dịch vừa tạo
                var transaction = await _db.WalletTransactions
                    .Where(wt => wt.UserId == session.Driver.UserId && 
                                wt.ReferenceId == session.SessionId && 
                                wt.TransactionType == "debit")
                    .OrderByDescending(wt => wt.CreatedAt)
                    .FirstOrDefaultAsync();

                // Lấy lại invoice với các items
                var invoiceWithItems = await _db.Invoices
                    .Include(i => i.InvoiceItems)
                        .ThenInclude(item => item.Session!)
                            .ThenInclude(s => s.Point)
                                .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoice.InvoiceId);

                // Tạo response hóa đơn
                var invoiceResponse = new InvoiceResponseDto
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    UserId = invoice.UserId,
                    TotalAmount = invoice.TotalAmount,
                    Status = invoice.Status,
                    CreatedAt = invoice.CreatedAt,
                    PaidAt = invoice.PaidAt,
                    Items = invoiceWithItems?.InvoiceItems.Select(item => new InvoiceItemDto
                    {
                        ItemId = item.ItemId,
                        SessionId = item.SessionId,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Amount = item.Amount
                    }).ToList() ?? new List<InvoiceItemDto>(),
                    SessionInfo = new SessionInfoDto
                    {
                        SessionId = session.SessionId,
                        StationName = session.Point.Station.Name,
                        StationAddress = session.Point.Station.Address,
                        EnergyUsed = session.EnergyUsed,
                        DurationMinutes = session.DurationMinutes,
                        CostBeforeDiscount = session.CostBeforeDiscount,
                        AppliedDiscount = session.AppliedDiscount,
                        FinalCost = session.FinalCost,
                        StartTime = session.StartTime,
                        EndTime = session.EndTime
                    }
                };

                return Ok(new 
                { 
                    message = "Thanh toán thành công",
                    success = true,
                    paymentInfo = new
                    {
                        paymentId = payment.PaymentId,
                        sessionId = session.SessionId,
                        userId = session.Driver.UserId,
                        amount = session.FinalCost.Value,
                        paymentMethod = "wallet",
                        paymentStatus = "success",
                        invoiceNumber = invoiceNumber,
                        paidAt = payment.CreatedAt,
                        transactionId = transaction?.TransactionId
                    },
                    walletInfo = new
                    {
                        balanceBefore = currentBalance,
                        amountDeducted = session.FinalCost.Value,
                        balanceAfter = newBalance
                    },
                    invoice = invoiceResponse
                });
            }
            catch (InvalidOperationException ex)
            {
                // Xử lý lỗi số dư không đủ hoặc user not found
                return BadRequest(new 
                { 
                    message = ex.Message,
                    error = "Payment processing failed"
                });
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

                // Lấy thông tin phiên sạc
                var session = await _db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

                if (session == null)
                {
                    return NotFound(new { message = $"Không tìm thấy phiên sạc với SessionId: {request.SessionId}" });
                }

                // Kiểm tra người dùng có phải chủ sở hữu phiên sạc không
                if (session.Driver == null || session.Driver.UserId != currentUserId)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền thanh toán phiên sạc này. Phiên sạc này thuộc về người dùng khác." });
                }

                // Kiểm tra phiên sạc đã hoàn thành chưa
                if (session.Status != "completed")
                {
                    return BadRequest(new 
                    { 
                        message = $"Phiên sạc chưa hoàn thành. Trạng thái hiện tại: {session.Status}. Vui lòng đợi phiên sạc hoàn thành trước khi thanh toán." 
                    });
                }

                // Kiểm tra đã có chi phí cuối cùng chưa
                if (!session.FinalCost.HasValue || session.FinalCost.Value <= 0)
                {
                    return BadRequest(new 
                    { 
                        message = "Phiên sạc chưa có chi phí cuối cùng. Vui lòng đợi hệ thống tính toán chi phí." 
                    });
                }

                // Kiểm tra đã thanh toán chưa (cả ví và tiền mặt)
                var alreadyPaid = await _db.Payments
                    .AnyAsync(p => p.SessionId == session.SessionId && 
                                  p.PaymentStatus == "success");

                if (alreadyPaid)
                {
                    var existingPayment = await _db.Payments
                        .Where(p => p.SessionId == session.SessionId && p.PaymentStatus == "success")
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();

                    return Ok(new 
                    { 
                        message = "Phiên sạc đã được thanh toán rồi",
                        alreadyPaid = true,
                        paymentInfo = new
                        {
                            paymentId = existingPayment?.PaymentId,
                            paymentMethod = existingPayment?.PaymentMethod,
                            amount = existingPayment?.Amount,
                            paidAt = existingPayment?.CreatedAt,
                            sessionId = session.SessionId
                        }
                    });
                }

                // Tạo số hóa đơn
                var invoiceNumber = $"INV-CASH-{session.SessionId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);

                // Tạo Invoice
                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    UserId = session.Driver.UserId,
                    BillingPeriodStart = currentDate,
                    BillingPeriodEnd = currentDate,
                    TotalAmount = session.FinalCost.Value,
                    Status = "paid",
                    DueDate = currentDate,
                    CreatedAt = DateTime.UtcNow,
                    PaidAt = DateTime.UtcNow
                };

                _db.Invoices.Add(invoice);
                await _db.SaveChangesAsync();

                // Tạo InvoiceItem
                var invoiceItem = new InvoiceItem
                {
                    InvoiceId = invoice.InvoiceId,
                    SessionId = session.SessionId,
                    Description = $"Phiên sạc #{session.SessionId} - Trạm: {session.Point.Station.Name}",
                    Quantity = 1,
                    UnitPrice = session.FinalCost.Value,
                    Amount = session.FinalCost.Value
                };

                _db.InvoiceItems.Add(invoiceItem);
                await _db.SaveChangesAsync();

                // Tạo bản ghi Payment với phương thức tiền mặt
                var payment = new Payment
                {
                    UserId = session.Driver.UserId,
                    SessionId = session.SessionId,
                    Amount = session.FinalCost.Value,
                    PaymentMethod = "cash",
                    PaymentStatus = "success",
                    InvoiceNumber = invoiceNumber,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                // Lấy lại invoice với các items
                var invoiceWithItems = await _db.Invoices
                    .Include(i => i.InvoiceItems)
                        .ThenInclude(item => item.Session!)
                            .ThenInclude(s => s.Point)
                                .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoice.InvoiceId);

                // Tạo response hóa đơn
                var invoiceResponse = new InvoiceResponseDto
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    UserId = invoice.UserId,
                    TotalAmount = invoice.TotalAmount,
                    Status = invoice.Status,
                    CreatedAt = invoice.CreatedAt,
                    PaidAt = invoice.PaidAt,
                    Items = invoiceWithItems?.InvoiceItems.Select(item => new InvoiceItemDto
                    {
                        ItemId = item.ItemId,
                        SessionId = item.SessionId,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Amount = item.Amount
                    }).ToList() ?? new List<InvoiceItemDto>(),
                    SessionInfo = new SessionInfoDto
                    {
                        SessionId = session.SessionId,
                        StationName = session.Point.Station.Name,
                        StationAddress = session.Point.Station.Address,
                        EnergyUsed = session.EnergyUsed,
                        DurationMinutes = session.DurationMinutes,
                        CostBeforeDiscount = session.CostBeforeDiscount,
                        AppliedDiscount = session.AppliedDiscount,
                        FinalCost = session.FinalCost,
                        StartTime = session.StartTime,
                        EndTime = session.EndTime
                    }
                };

                return Ok(new 
                { 
                    message = "Thanh toán tiền mặt thành công",
                    success = true,
                    paymentInfo = new
                    {
                        paymentId = payment.PaymentId,
                        sessionId = session.SessionId,
                        userId = session.Driver.UserId,
                        amount = session.FinalCost.Value,
                        paymentMethod = "cash",
                        paymentStatus = "success",
                        invoiceNumber = invoiceNumber,
                        paidAt = payment.CreatedAt
                    },
                    invoice = invoiceResponse
                });
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

