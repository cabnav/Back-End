using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Admin.Implementations;

/// <summary>
/// Service quản lý giá tiền cọc (Deposit) - chỉ dành cho Admin
/// </summary>
public class DepositService : IDepositService
{
    private readonly EvchargingManagementContext _db;

    public DepositService(EvchargingManagementContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lấy giá tiền cọc hiện tại (record có status = "active")
    /// </summary>
    public async Task<decimal> GetCurrentDepositAmountAsync()
    {
        var activeDeposit = await _db.Deposits
            .Where(d => d.Status == "active")
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        // Nếu không có record active, trả về giá mặc định 20000
        return activeDeposit?.Amount ?? 20000m;
    }

    /// <summary>
    /// Lấy thông tin chi tiết về giá tiền cọc hiện tại
    /// </summary>
    public async Task<DepositInfo?> GetCurrentDepositInfoAsync()
    {
        var activeDeposit = await _db.Deposits
            .Where(d => d.Status == "active")
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        if (activeDeposit == null)
            return null;

        return new DepositInfo
        {
            DepositId = activeDeposit.DepositId,
            Amount = activeDeposit.Amount,
            Description = activeDeposit.Description,
            Status = activeDeposit.Status,
            CreatedAt = activeDeposit.CreatedAt,
            UpdatedAt = activeDeposit.UpdatedAt
        };
    }

    /// <summary>
    /// Cập nhật giá tiền cọc mới
    /// </summary>
    public async Task<DepositInfo> UpdateDepositAmountAsync(decimal newAmount, string? description = null)
    {
        // Validate giá mới
        if (newAmount <= 0)
            throw new ArgumentException("Giá tiền cọc phải lớn hơn 0", nameof(newAmount));

        // Set tất cả record active thành inactive
        var activeDeposits = await _db.Deposits
            .Where(d => d.Status == "active")
            .ToListAsync();

        foreach (var deposit in activeDeposits)
        {
            deposit.Status = "inactive";
            deposit.UpdatedAt = DateTime.UtcNow;
        }

        // Tạo record mới với status = "active"
        var newDeposit = new Deposit
        {
            Amount = newAmount,
            Description = description ?? $"Giá tiền cọc được cập nhật bởi admin vào {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Deposits.Add(newDeposit);
        await _db.SaveChangesAsync();

        return new DepositInfo
        {
            DepositId = newDeposit.DepositId,
            Amount = newDeposit.Amount,
            Description = newDeposit.Description,
            Status = newDeposit.Status,
            CreatedAt = newDeposit.CreatedAt,
            UpdatedAt = newDeposit.UpdatedAt
        };
    }

    /// <summary>
    /// Lấy lịch sử thay đổi giá tiền cọc
    /// </summary>
    public async Task<IEnumerable<DepositInfo>> GetDepositHistoryAsync()
    {
        var deposits = await _db.Deposits
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return deposits.Select(d => new DepositInfo
        {
            DepositId = d.DepositId,
            Amount = d.Amount,
            Description = d.Description,
            Status = d.Status,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        });
    }
}

