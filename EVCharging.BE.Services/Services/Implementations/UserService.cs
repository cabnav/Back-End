using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly EvchargingManagementContext _db;

        public UserService(EvchargingManagementContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
            => await _db.Users.ToListAsync();

        public async Task<User?> GetByIdAsync(int id)
            => await _db.Users.FindAsync(id);

        public async Task<User> CreateAsync(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<bool> UpdateAsync(int id, User updated)
        {
            if (id != updated.UserId) return false;

            _db.Users.Update(updated);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return false;

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return true;
        }

        // ==========================
        //      ✅ THÊM MỚI
        // ==========================

        /// <summary>
        /// Nạp tiền vào ví người dùng, tạo 1 bản ghi WalletTransaction và trả về DTO.
        /// </summary>
        public async Task<WalletTransactionDTO> WalletTopUpAsync(int userId, decimal amount, string? description)
        {
            if (amount <= 0) throw new ArgumentException("Số tiền nạp phải > 0");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
                       ?? throw new KeyNotFoundException("User không tồn tại");

            await using var tx = await _db.Database.BeginTransactionAsync();

            user.WalletBalance = (user.WalletBalance ?? 0m) + amount;

            var now = DateTime.UtcNow;
            var txRow = new WalletTransaction
            {
                UserId = userId,
                Amount = amount,
                TransactionType = "top_up",
                Description = string.IsNullOrWhiteSpace(description) ? "Top-up" : description,
                BalanceAfter = user.WalletBalance ?? 0m,
                ReferenceId = null,
                CreatedAt = now
            };

            _db.WalletTransactions.Add(txRow);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new WalletTransactionDTO
            {
                TransactionId = txRow.TransactionId,
                UserId = userId,
                Amount = amount,
                TransactionType = "top_up",
                Description = txRow.Description ?? "",
                BalanceAfter = txRow.BalanceAfter,
                ReferenceId = null,
                CreatedAt = now
            };
        }

        /// <summary>
        /// Lấy lịch sử giao dịch ví của user (mới nhất trước), có phân trang đơn giản.
        /// </summary>
        public async Task<IEnumerable<WalletTransactionDTO>> GetWalletTransactionsAsync(int userId, int page = 1, int pageSize = 50)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 50;

            var query = _db.WalletTransactions
                           .AsNoTracking()
                           .Where(t => t.UserId == userId)
                           .OrderByDescending(t => t.CreatedAt);

            var list = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return list.Select(t => new WalletTransactionDTO
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                Amount = t.Amount,
                TransactionType = t.TransactionType ?? "",
                Description = t.Description ?? "",
                BalanceAfter = t.BalanceAfter,
                ReferenceId = t.ReferenceId,
                CreatedAt = t.CreatedAt ?? DateTime.UtcNow
            });
        }

        /// <summary>
        /// Cập nhật hồ sơ User và DriverProfile (nếu có).
        /// Chỉ ghi đè các field có giá trị.
        /// </summary>
        public async Task<bool> UpdateUserProfileAsync(int userId, UserUpdateRequest req)
        {
            var user = await _db.Users
                                .Include(u => u.DriverProfile)
                                .FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return false;

            // Update trường của User
            if (!string.IsNullOrWhiteSpace(req.Name)) user.Name = req.Name;
            if (!string.IsNullOrWhiteSpace(req.Phone)) user.Phone = req.Phone;

            // Update DriverProfile (nếu có)
            if (user.DriverProfile != null)
            {
                if (!string.IsNullOrWhiteSpace(req.LicenseNumber))
                    user.DriverProfile.LicenseNumber = req.LicenseNumber;

                if (!string.IsNullOrWhiteSpace(req.VehicleModel))
                    user.DriverProfile.VehicleModel = req.VehicleModel;

                if (!string.IsNullOrWhiteSpace(req.VehiclePlate))
                    user.DriverProfile.VehiclePlate = req.VehiclePlate;

                if (req.BatteryCapacity.HasValue)
                    user.DriverProfile.BatteryCapacity = req.BatteryCapacity.Value;
            }

            await _db.SaveChangesAsync();
            return true;
        }
    }
}
