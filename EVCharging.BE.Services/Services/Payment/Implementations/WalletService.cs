using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Payment.Implementations
{
    public class WalletService : IWalletService
    {
        private readonly EvchargingManagementContext _db;

        public WalletService(EvchargingManagementContext db) => _db = db;

        public async Task<decimal> GetBalanceAsync(int userId)
        {
            var bal = await _db.Users
                .Where(u => u.UserId == userId)
                .Select(u => u.WalletBalance)
                .FirstOrDefaultAsync();

            return bal ?? 0m;
        }

        public async Task<(IEnumerable<WalletTransaction> Items, int Total)>
            GetTransactionsAsync(int userId, int skip = 0, int take = 20,
                                 DateTime? from = null, DateTime? to = null, string? type = null)
        {
            take = (take <= 0 || take > 200) ? 20 : take; // clamp nhẹ

            var q = _db.WalletTransactions
                       .AsNoTracking()
                       .Where(w => w.UserId == userId);

            if (from.HasValue) q = q.Where(w => w.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(w => w.CreatedAt <= to.Value);
            if (!string.IsNullOrWhiteSpace(type)) q = q.Where(w => w.TransactionType == type);

            var total = await q.CountAsync();

            var items = await q.OrderByDescending(w => w.CreatedAt)
                               .Skip(skip)
                               .Take(take)
                               .ToListAsync();

            return (items, total);
        }

        public async Task CreditAsync(int userId, decimal amount, string description, int? referenceId = null)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be > 0");

            // ⚠️ Quan trọng: bọc toàn bộ transaction trong execution strategy
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == userId);
                    if (user == null) throw new InvalidOperationException("User not found");

                    var oldBal = user.WalletBalance ?? 0m;
                    var newBal = oldBal + amount;
                    user.WalletBalance = newBal;

                    _db.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = userId,
                        Amount = amount,
                        TransactionType = "topup",
                        Description = description,
                        BalanceAfter = newBal,
                        ReferenceId = referenceId,
                        CreatedAt = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task DebitAsync(int userId, decimal amount, string description, int? referenceId = null)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be > 0");

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == userId);
                    if (user == null) throw new InvalidOperationException("User not found");

                    var oldBal = user.WalletBalance ?? 0m;
                    if (oldBal < amount) throw new InvalidOperationException("Insufficient wallet balance");

                    var newBal = oldBal - amount;
                    user.WalletBalance = newBal;

                    _db.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = userId,
                        Amount = -amount,
                        TransactionType = "debit",
                        Description = description,
                        BalanceAfter = newBal,
                        ReferenceId = referenceId,
                        CreatedAt = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });
        }
    }
}
