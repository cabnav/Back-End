using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using EVCharging.BE.Services.Services.Payment; // IMockPayService
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

namespace EVCharging.BE.Services.Services.Payment.Implementations
{
    public class MockPayService : IMockPayService
    {
        private readonly EvchargingManagementContext _db;
        public MockPayService(EvchargingManagementContext db) => _db = db;

        public async Task<(string Code, string QrBase64, DateTime ExpiresAt)> CreateTopUpAsync(int userId, decimal amount)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be > 0");

            // (Optional) đảm bảo user tồn tại để tránh lỗi FK
            if (!await _db.Users.AnyAsync(u => u.UserId == userId))
                throw new InvalidOperationException("User not found");

            var code = $"TP{Guid.NewGuid():N}".ToUpperInvariant();
            var expires = DateTime.UtcNow.AddMinutes(10);

            _db.Payments.Add(new PaymentEntity
            {
                UserId = userId,
                Amount = amount,
                PaymentMethod = "mock",
                PaymentStatus = "pending",
                InvoiceNumber = code,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // QR tạm (controller sẽ build absolute URL khi trả ra)
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode($"/mockpay/checkout/{code}", QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(8);
            var base64 = Convert.ToBase64String(png);

            return (code, base64, expires);
        }

        public async Task<bool> ConfirmAsync(string code, bool success)
        {
            var pay = await _db.Payments
                .FirstOrDefaultAsync(p => p.InvoiceNumber == code && p.PaymentMethod == "mock");
            if (pay is null) return false;

            // Idempotent: nếu đã ở trạng thái cuối thì coi như ok
            if (pay.PaymentStatus is "success" or "failed") return true;

            // Bọc toàn bộ trong execution strategy + transaction (vì có EnableRetryOnFailure)
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    if (!success)
                    {
                        // Thất bại: chỉ cập nhật trạng thái rồi commit
                        pay.PaymentStatus = "failed";
                        await _db.SaveChangesAsync();
                        await tx.CommitAsync();
                        return;
                    }

                    // Thành công: cộng ví + ghi giao dịch + đặt trạng thái success, tất cả cùng transaction
                    var user = await _db.Users.SingleAsync(u => u.UserId == pay.UserId);

                    var newBal = (user.WalletBalance ?? 0m) + pay.Amount;
                    user.WalletBalance = newBal;

                    _db.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = user.UserId,
                        Amount = pay.Amount,
                        TransactionType = "topup",
                        Description = $"MockPay:{code}",
                        BalanceAfter = newBal,
                        ReferenceId = pay.PaymentId,
                        CreatedAt = DateTime.UtcNow
                    });

                    pay.PaymentStatus = "success";
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return true;
        }

        public Task<string?> GetStatusAsync(string code) =>
            _db.Payments
               .Where(p => p.InvoiceNumber == code && p.PaymentMethod == "mock")
               .Select(p => p.PaymentStatus)
               .FirstOrDefaultAsync();
    }
}
