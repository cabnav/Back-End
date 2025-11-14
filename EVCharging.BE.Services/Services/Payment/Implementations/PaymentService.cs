using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;


namespace EVCharging.BE.Services.Services.Payment.Implementations
{
    /// <summary>
    /// Service quản lý thanh toán cho phiên sạc
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IWalletService _walletService;
        private readonly IInvoiceService _invoiceService;

        public PaymentService(
            EvchargingManagementContext db,
            IWalletService walletService,
            IInvoiceService invoiceService)
        {
            _db = db;
            _walletService = walletService;
            _invoiceService = invoiceService;
        }

        /// <summary>
        /// Thanh toán phiên sạc bằng ví (có xử lý đặt cọc)
        /// </summary>
        public async Task<PaymentResultDto> PayByWalletAsync(int sessionId, int userId)
        {
            // Lấy thông tin phiên sạc
            var session = await _db.ChargingSessions
                .Include(s => s.Driver)
                    .ThenInclude(d => d.User)
                .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
                throw new InvalidOperationException($"Không tìm thấy phiên sạc với SessionId: {sessionId}");

            // Kiểm tra quyền sở hữu
            if (session.Driver?.UserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền thanh toán phiên sạc này.");

            // Kiểm tra phiên sạc đã hoàn thành chưa
            if (session.Status != "completed")
                throw new InvalidOperationException($"Phiên sạc chưa hoàn thành. Trạng thái hiện tại: {session.Status}");

            // Kiểm tra đã có chi phí cuối cùng chưa
            if (!session.FinalCost.HasValue || session.FinalCost.Value <= 0)
                throw new InvalidOperationException("Phiên sạc chưa có chi phí cuối cùng.");

            // Kiểm tra đã thanh toán chưa
            var existingPayment = await _db.Payments
                .Where(p => p.SessionId == sessionId 
                    && p.PaymentStatus == "success" 
                    && p.PaymentType == "session_payment")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingPayment != null)
            {
                var existingTransaction = await _db.WalletTransactions
                    .Where(wt => wt.UserId == userId &&
                               wt.ReferenceId == sessionId &&
                               wt.TransactionType == "debit")
                    .OrderByDescending(wt => wt.CreatedAt)
                    .FirstOrDefaultAsync();

                return new PaymentResultDto
                {
                    Success = false,
                    AlreadyPaid = true,
                    Message = "Phiên sạc đã được thanh toán rồi",
                    ExistingPaymentInfo = new PaymentInfoDto
                    {
                        PaymentId = existingPayment.PaymentId,
                        PaymentMethod = existingPayment.PaymentMethod ?? "",
                        Amount = existingPayment.Amount,
                        InvoiceNumber = existingPayment.InvoiceNumber,
                        PaidAt = existingPayment.CreatedAt,
                        SessionId = existingPayment.SessionId,
                        ReservationId = existingPayment.ReservationId,
                        UserId = userId,
                        TransactionId = existingTransaction?.TransactionId
                    }
                };
            }

            // Tìm reservation thông qua payment deposit (cùng pointId và driverId)
            decimal depositAmount = 0;
            PaymentEntity? depositPayment = null;
            int? reservationId = null;

            // Tìm reservation của cùng driver và point, trong khoảng thời gian session
            var reservation = await _db.Reservations
                .Where(r => r.DriverId == session.DriverId
                    && r.PointId == session.PointId
                    && r.StartTime <= session.StartTime
                    && r.EndTime >= session.StartTime
                    && (r.Status == "booked" || r.Status == "completed" || r.Status == "confirmed"))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (reservation != null)
            {
                reservationId = reservation.ReservationId;
                
                // Tìm deposit payment của reservation này
                depositPayment = await _db.Payments
                    .Where(p => p.ReservationId == reservation.ReservationId
                        && p.PaymentType == "deposit"
                        && p.PaymentStatus == "success")
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (depositPayment != null)
                {
                    depositAmount = depositPayment.Amount;
                }
            }

            // Tính số tiền cần thanh toán (FinalCost - Deposit)
            var amountToPay = session.FinalCost.Value - depositAmount;
            if (amountToPay < 0)
            {
                // Nếu deposit > FinalCost, hoàn tiền dư vào ví
                var refundAmount = Math.Abs(amountToPay);
                await _walletService.CreditAsync(
                    userId,
                    refundAmount,
                    $"Hoàn tiền cọc dư cho phiên sạc #{sessionId}",
                    sessionId
                );
                amountToPay = 0; // Không cần thanh toán thêm
            }

            // Kiểm tra số dư ví (chỉ nếu cần thanh toán thêm)
            var currentBalance = await _walletService.GetBalanceAsync(userId);
            if (amountToPay > 0 && currentBalance < amountToPay)
                throw new InvalidOperationException($"Số dư ví không đủ. Số dư hiện tại: {currentBalance:F0} VND, Cần: {amountToPay:F0} VND (Đã trừ cọc {depositAmount:F0} VND)");

            // Trừ tiền từ ví (chỉ nếu cần thanh toán thêm)
            decimal newBalance = currentBalance;
            if (amountToPay > 0)
            {
                var paymentDescription = $"Thanh toán phiên sạc #{sessionId} - Trạm: {session.Point.Station?.Name ?? "N/A"} (Đã trừ cọc {depositAmount:F0} VND)";
                await _walletService.DebitAsync(
                    userId,
                    amountToPay,
                    paymentDescription,
                    sessionId
                );
                newBalance = await _walletService.GetBalanceAsync(userId);
            }

            // Tạo invoice
            var invoice = await _invoiceService.CreateInvoiceForSessionAsync(
                sessionId,
                userId,
                amountToPay > 0 ? amountToPay : session.FinalCost.Value,
                "wallet"
            );

            // Tạo bản ghi Payment
            var payment = new PaymentEntity
            {
                UserId = userId,
                SessionId = sessionId,
                ReservationId = reservationId,
                Amount = amountToPay > 0 ? amountToPay : session.FinalCost.Value,
                PaymentMethod = "wallet",
                PaymentStatus = "success",
                PaymentType = "session_payment", // ⭐ Quan trọng
                InvoiceNumber = invoice.InvoiceNumber,
                CreatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            // Lấy transaction vừa tạo
            var transaction = await _db.WalletTransactions
                .Where(wt => wt.UserId == userId &&
                           wt.ReferenceId == sessionId &&
                           wt.TransactionType == "debit")
                .OrderByDescending(wt => wt.CreatedAt)
                .FirstOrDefaultAsync();

            var successMessage = depositAmount > 0
                ? $"Thanh toán thành công. Đã trừ cọc {depositAmount:F0} VND, thanh toán thêm {amountToPay:F0} VND"
                : "Thanh toán thành công";

            return new PaymentResultDto
            {
                Success = true,
                Message = successMessage,
                PaymentInfo = new PaymentInfoDto
                {
                    PaymentId = payment.PaymentId,
                    SessionId = sessionId,
                    ReservationId = reservationId,
                    UserId = userId,
                    Amount = amountToPay > 0 ? amountToPay : session.FinalCost.Value,
                    PaymentMethod = "wallet",
                    PaymentStatus = "success",
                    InvoiceNumber = invoice.InvoiceNumber,
                    PaidAt = payment.CreatedAt,
                    TransactionId = transaction?.TransactionId
                },
                WalletInfo = new WalletInfoDto
                {
                    BalanceBefore = currentBalance,
                    AmountDeducted = amountToPay > 0 ? amountToPay : 0,
                    BalanceAfter = newBalance
                },
                Invoice = invoice
            };
        }

        /// <summary>
        /// Thanh toán phiên sạc bằng tiền mặt (tạo payment pending, chờ staff xác nhận)
        /// </summary>
        public async Task<PaymentResultDto> PayByCashAsync(int sessionId, int userId)
        {
            // Lấy thông tin phiên sạc
            var session = await _db.ChargingSessions
                .Include(s => s.Driver)
                    .ThenInclude(d => d.User)
                .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
                throw new InvalidOperationException($"Không tìm thấy phiên sạc với SessionId: {sessionId}");

            // Kiểm tra quyền sở hữu
            if (session.Driver?.UserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền thanh toán phiên sạc này.");

            // Kiểm tra phiên sạc đã hoàn thành chưa
            if (session.Status != "completed")
                throw new InvalidOperationException($"Phiên sạc chưa hoàn thành. Trạng thái hiện tại: {session.Status}");

            // Kiểm tra đã có chi phí cuối cùng chưa
            if (!session.FinalCost.HasValue || session.FinalCost.Value <= 0)
                throw new InvalidOperationException("Phiên sạc chưa có chi phí cuối cùng.");

            // Kiểm tra đã thanh toán chưa (check cả pending và success)
            var existingPayment = await _db.Payments
                .Where(p => p.SessionId == sessionId
                    && (p.PaymentStatus == "success" || p.PaymentStatus == "pending")
                    && p.PaymentType == "session_payment")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingPayment != null)
            {
                // Nếu payment đã success -> đã thanh toán rồi
                if (existingPayment.PaymentStatus == "success")
                {
                    return new PaymentResultDto
                    {
                        Success = false,
                        AlreadyPaid = true,
                        Message = "Phiên sạc đã được thanh toán rồi",
                        ExistingPaymentInfo = new PaymentInfoDto
                        {
                            PaymentId = existingPayment.PaymentId,
                            PaymentMethod = existingPayment.PaymentMethod ?? "",
                            Amount = existingPayment.Amount,
                            PaymentStatus = existingPayment.PaymentStatus ?? "",
                            InvoiceNumber = existingPayment.InvoiceNumber,
                            PaidAt = existingPayment.CreatedAt,
                            SessionId = existingPayment.SessionId,
                            ReservationId = existingPayment.ReservationId,
                            UserId = userId
                        }
                    };
                }
                
                // Nếu payment đang pending -> trả về thông tin payment pending
                return new PaymentResultDto
                {
                    Success = true,
                    AlreadyPaid = false,
                    Message = "Đang chờ thanh toán. Vui lòng thanh toán tại trạm.",
                    PaymentInfo = new PaymentInfoDto
                    {
                        PaymentId = existingPayment.PaymentId,
                        PaymentMethod = existingPayment.PaymentMethod ?? "",
                        Amount = existingPayment.Amount,
                        PaymentStatus = existingPayment.PaymentStatus ?? "",
                        InvoiceNumber = existingPayment.InvoiceNumber,
                        PaidAt = null,
                        SessionId = existingPayment.SessionId,
                        ReservationId = existingPayment.ReservationId,
                        UserId = userId
                    },
                    Invoice = null
                };
            }

            // Tìm reservation nếu có
            int? reservationId = null;
            var reservation = await _db.Reservations
                .Where(r => r.DriverId == session.DriverId
                    && r.PointId == session.PointId
                    && r.StartTime <= session.StartTime
                    && r.EndTime >= session.StartTime
                    && (r.Status == "booked" || r.Status == "completed" || r.Status == "confirmed"))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (reservation != null)
            {
                reservationId = reservation.ReservationId;
            }

            // Tạo bản ghi Payment với status = "pending"
            // Không tạo invoice ngay, invoice sẽ được tạo khi staff xác nhận thanh toán
            var payment = new PaymentEntity
            {
                UserId = userId,
                SessionId = sessionId,
                ReservationId = reservationId,
                Amount = session.FinalCost.Value,
                PaymentMethod = "cash",
                PaymentStatus = "pending", // ⭐ Đang chờ thanh toán
                PaymentType = "session_payment",
                InvoiceNumber = null, // Invoice sẽ được tạo khi staff xác nhận
                CreatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return new PaymentResultDto
            {
                Success = true,
                Message = "Đang chờ thanh toán. Vui lòng thanh toán tại trạm.",
                PaymentInfo = new PaymentInfoDto
                {
                    PaymentId = payment.PaymentId,
                    SessionId = sessionId,
                    ReservationId = reservationId,
                    UserId = userId,
                    Amount = session.FinalCost.Value,
                    PaymentMethod = "cash",
                    PaymentStatus = "pending",
                    InvoiceNumber = null,
                    PaidAt = null
                },
                Invoice = null // Invoice sẽ được tạo khi staff xác nhận
            };
        }

        /// <summary>
        /// Lấy danh sách sessions chưa thanh toán của user (để user check và bấm thanh toán)
        /// </summary>
        public async Task<UnpaidSessionsResponse> GetUnpaidSessionsAsync(int userId, int skip = 0, int take = 20)
        {
            // Lấy sessions chưa thanh toán (completed nhưng chưa có payment success)
            var paidSessionIds = await _db.Payments
                .Where(p => p.PaymentStatus == "success" 
                    && p.PaymentType == "session_payment"
                    && p.SessionId.HasValue)
                .Select(p => p.SessionId!.Value)
                .Distinct()
                .ToListAsync();

            var unpaidSessions = await _db.ChargingSessions
                .Include(s => s.Driver)
                .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                .Where(s => s.Driver.UserId == userId
                    && s.Status == "completed"
                    && s.FinalCost.HasValue
                    && s.FinalCost.Value > 0
                    && !paidSessionIds.Contains(s.SessionId))
                .OrderByDescending(s => s.EndTime ?? s.StartTime)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var unpaidTotal = await _db.ChargingSessions
                .Where(s => s.Driver.UserId == userId
                    && s.Status == "completed"
                    && s.FinalCost.HasValue
                    && s.FinalCost.Value > 0
                    && !paidSessionIds.Contains(s.SessionId))
                .CountAsync();

            // Lấy pending payments cho các unpaid sessions
            var unpaidSessionIds = unpaidSessions.Select(s => s.SessionId).ToList();
            var pendingPayments = await _db.Payments
                .Where(p => unpaidSessionIds.Contains(p.SessionId!.Value)
                    && p.PaymentStatus == "pending"
                    && p.PaymentType == "session_payment")
                .ToListAsync();
            var pendingPaymentsDict = pendingPayments
                .GroupBy(p => p.SessionId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            // Map unpaid sessions
            var unpaidSessionsList = unpaidSessions.Select(s =>
            {
                var pendingPayment = pendingPaymentsDict.GetValueOrDefault(s.SessionId);

                return new UnpaidSessionDto
                {
                    SessionId = s.SessionId,
                    DriverId = s.DriverId,
                    PointId = s.PointId,
                    ReservationId = s.ReservationId,
                    Status = s.Status,
                    StationId = s.Point?.StationId,
                    StationName = s.Point?.Station?.Name,
                    StationAddress = s.Point?.Station?.Address,
                    ConnectorType = s.Point?.ConnectorType,
                    PowerOutput = s.Point?.PowerOutput,
                    PricePerKwh = s.Point?.PricePerKwh,
                    InitialSoc = s.InitialSoc,
                    FinalSoc = s.FinalSoc,
                    EnergyUsed = s.EnergyUsed,
                    DurationMinutes = s.DurationMinutes,
                    CostBeforeDiscount = s.CostBeforeDiscount,
                    AppliedDiscount = s.AppliedDiscount,
                    FinalCost = s.FinalCost,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Notes = s.Notes,
                    PaymentId = pendingPayment?.PaymentId,
                    PaymentMethod = pendingPayment?.PaymentMethod,
                    PaymentStatus = pendingPayment?.PaymentStatus ?? "none",
                    HasPendingPayment = pendingPayment != null
                };
            }).ToList();

            return new UnpaidSessionsResponse
            {
                Total = unpaidTotal,
                Skip = skip,
                Take = take,
                Items = unpaidSessionsList
            };
        }

        /// <summary>
        /// Lấy danh sách invoices đã thanh toán của user
        /// </summary>
        public async Task<PaidInvoicesResponse> GetPaidInvoicesAsync(int userId, int skip = 0, int take = 20)
        {
            // Lấy danh sách invoiceNumbers của user có payment success
            var paidInvoiceNumbers = await _db.Payments
                .Where(p => p.UserId == userId
                    && p.PaymentStatus == "success"
                    && !string.IsNullOrEmpty(p.InvoiceNumber))
                .Select(p => p.InvoiceNumber!)
                .Distinct()
                .ToListAsync();

            var paidInvoices = await _db.Invoices
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Point)
                            .ThenInclude(p => p.Station)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Driver)
                .Where(i => i.UserId == userId
                    && paidInvoiceNumbers.Contains(i.InvoiceNumber))
                .OrderByDescending(i => i.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var paidTotal = await _db.Invoices
                .Where(i => i.UserId == userId
                    && paidInvoiceNumbers.Contains(i.InvoiceNumber))
                .CountAsync();

            // Lấy payments cho các invoices (tối ưu query)
            var invoiceNumbers = paidInvoices.Select(i => i.InvoiceNumber).ToList();
            var payments = await _db.Payments
                .Where(p => invoiceNumbers.Contains(p.InvoiceNumber!) 
                    && p.PaymentStatus == "success")
                .ToListAsync();
            var paymentsDict = payments
                .GroupBy(p => p.InvoiceNumber!)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First());

            // Map paid invoices
            var paidInvoicesList = paidInvoices.Select(invoice =>
            {
                var firstItem = invoice.InvoiceItems.FirstOrDefault();
                var session = firstItem?.Session;
                var payment = paymentsDict.GetValueOrDefault(invoice.InvoiceNumber);

                return new PaidInvoiceDto
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    TotalAmount = invoice.TotalAmount,
                    Status = invoice.Status,
                    PaymentMethod = payment?.PaymentMethod,
                    PaymentStatus = payment?.PaymentStatus,
                    PaymentId = payment?.PaymentId,
                    CreatedAt = invoice.CreatedAt,
                    PaidAt = invoice.PaidAt,
                    SessionInfo = session != null ? new SessionInfoDto
                    {
                        SessionId = session.SessionId,
                        DriverId = session.DriverId,
                        PointId = session.PointId,
                        ReservationId = session.ReservationId,
                        Status = session.Status,
                        StationId = session.Point?.StationId,
                        StationName = session.Point?.Station?.Name,
                        StationAddress = session.Point?.Station?.Address,
                        ConnectorType = session.Point?.ConnectorType,
                        PowerOutput = session.Point?.PowerOutput,
                        PricePerKwh = session.Point?.PricePerKwh,
                        InitialSoc = session.InitialSoc,
                        FinalSoc = session.FinalSoc,
                        EnergyUsed = session.EnergyUsed,
                        DurationMinutes = session.DurationMinutes,
                        CostBeforeDiscount = session.CostBeforeDiscount,
                        AppliedDiscount = session.AppliedDiscount,
                        FinalCost = session.FinalCost,
                        StartTime = session.StartTime,
                        EndTime = session.EndTime,
                        Notes = session.Notes
                    } : null
                };
            }).ToList();

            return new PaidInvoicesResponse
            {
                Total = paidTotal,
                Skip = skip,
                Take = take,
                Items = paidInvoicesList
            };
        }
    }
}

