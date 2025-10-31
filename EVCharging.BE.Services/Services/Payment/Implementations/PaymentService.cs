using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

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
                        SessionId = sessionId,
                        TransactionId = existingTransaction?.TransactionId
                    }
                };
            }

            // Kiểm tra số dư ví
            var currentBalance = await _walletService.GetBalanceAsync(userId);
            if (currentBalance < session.FinalCost.Value)
                throw new InvalidOperationException($"Số dư ví không đủ. Số dư hiện tại: {currentBalance:F0} VND, Cần: {session.FinalCost.Value:F0} VND");

            // Trừ tiền từ ví
            var paymentDescription = $"Thanh toán phiên sạc #{sessionId} - Trạm: {session.Point.Station.Name}";
            await _walletService.DebitAsync(
                userId,
                session.FinalCost.Value,
                paymentDescription,
                sessionId
            );

            var newBalance = await _walletService.GetBalanceAsync(userId);

            // Tạo hóa đơn
            var invoice = await _invoiceService.CreateInvoiceForSessionAsync(
                sessionId,
                userId,
                session.FinalCost.Value,
                "wallet"
            );

            // Tạo bản ghi Payment
            var payment = new Payment
            {
                UserId = userId,
                SessionId = sessionId,
                Amount = session.FinalCost.Value,
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

            return new PaymentResultDto
            {
                Success = true,
                Message = "Thanh toán thành công",
                PaymentInfo = new PaymentInfoDto
                {
                    PaymentId = payment.PaymentId,
                    SessionId = sessionId,
                    UserId = userId,
                    Amount = session.FinalCost.Value,
                    PaymentMethod = "wallet",
                    PaymentStatus = "success",
                    InvoiceNumber = invoice.InvoiceNumber,
                    PaidAt = payment.CreatedAt,
                    TransactionId = transaction?.TransactionId
                },
                WalletInfo = new WalletInfoDto
                {
                    BalanceBefore = currentBalance,
                    AmountDeducted = session.FinalCost.Value,
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
                        SessionId = sessionId
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
            var payment = new Payment
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

