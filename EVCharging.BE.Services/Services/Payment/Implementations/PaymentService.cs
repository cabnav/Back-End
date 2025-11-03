using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
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
                .Where(p => p.SessionId == sessionId && p.PaymentStatus == "success")
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

            // Kiểm tra xem có deposit từ reservation không
            decimal depositAmount = 0;
            PaymentEntity? depositPayment = null;
            if (session.ReservationId.HasValue)
            {
                depositPayment = await _db.Payments
                    .Where(p => p.ReservationId == session.ReservationId.Value &&
                               p.PaymentStatus == "success" &&
                               p.Amount == 20000m)
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
                var paymentDescription = $"Thanh toán phiên sạc #{sessionId} - Trạm: {session.Point.Station.Name} (Đã trừ cọc {depositAmount:F0} VND)";
                await _walletService.DebitAsync(
                    userId,
                    amountToPay,
                    paymentDescription,
                    sessionId
                );
                newBalance = await _walletService.GetBalanceAsync(userId);
            }

            // Tạo hóa đơn với số tiền thực tế cần thanh toán (có thể là 0 nếu deposit đủ)
            var invoice = await _invoiceService.CreateInvoiceForSessionAsync(
                sessionId,
                userId,
                amountToPay > 0 ? amountToPay : session.FinalCost.Value, // Nếu không cần thanh toán thêm, vẫn tạo invoice với FinalCost để hiển thị đúng
                "wallet"
            );

            // Tạo bản ghi Payment
            var payment = new PaymentEntity
            {
                UserId = userId,
                SessionId = sessionId,
                Amount = amountToPay > 0 ? amountToPay : session.FinalCost.Value, // Lưu số tiền thực tế thanh toán
                PaymentMethod = "wallet",
                PaymentStatus = "success",
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
                    ReservationId = session.ReservationId,
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
                    AmountDeducted = amountToPay > 0 ? amountToPay : 0, // Chỉ hiển thị số tiền thực tế trừ
                    BalanceAfter = newBalance
                },
                Invoice = invoice
            };
        }

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

            // Kiểm tra đã thanh toán chưa
            var existingPayment = await _db.Payments
                .Where(p => p.SessionId == sessionId && p.PaymentStatus == "success")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingPayment != null)
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
                        InvoiceNumber = existingPayment.InvoiceNumber,
                        PaidAt = existingPayment.CreatedAt,
                        SessionId = existingPayment.SessionId,
                        ReservationId = existingPayment.ReservationId,
                        UserId = userId
                    }
                };
            }

            // Tạo hóa đơn
            var invoice = await _invoiceService.CreateInvoiceForSessionAsync(
                sessionId,
                userId,
                session.FinalCost.Value,
                "cash"
            );

            // Tạo bản ghi Payment
            var payment = new PaymentEntity
            {
                UserId = userId,
                SessionId = sessionId,
                Amount = session.FinalCost.Value,
                PaymentMethod = "cash",
                PaymentStatus = "success",
                InvoiceNumber = invoice.InvoiceNumber,
                CreatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return new PaymentResultDto
            {
                Success = true,
                Message = "Thanh toán tiền mặt thành công",
                PaymentInfo = new PaymentInfoDto
                {
                    PaymentId = payment.PaymentId,
                    SessionId = sessionId,
                    ReservationId = session.ReservationId,
                    UserId = userId,
                    Amount = session.FinalCost.Value,
                    PaymentMethod = "cash",
                    PaymentStatus = "success",
                    InvoiceNumber = invoice.InvoiceNumber,
                    PaidAt = payment.CreatedAt
                },
                Invoice = invoice
            };
        }
    }
}

