namespace EVCharging.BE.Services.Services.Admin;

/// <summary>
/// Service quản lý giá tiền cọc (Deposit) - chỉ dành cho Admin
/// </summary>
public interface IDepositService
{
    /// <summary>
    /// Lấy giá tiền cọc hiện tại (record có status = "active")
    /// </summary>
    Task<decimal> GetCurrentDepositAmountAsync();

    /// <summary>
    /// Lấy thông tin chi tiết về giá tiền cọc hiện tại
    /// </summary>
    Task<DepositInfo?> GetCurrentDepositInfoAsync();

    /// <summary>
    /// Cập nhật giá tiền cọc mới
    /// - Nếu có record active, sẽ set status = "inactive" và tạo record mới với status = "active"
    /// - Nếu chưa có record nào, sẽ tạo record mới với status = "active"
    /// </summary>
    Task<DepositInfo> UpdateDepositAmountAsync(decimal newAmount, string? description = null);

    /// <summary>
    /// Lấy lịch sử thay đổi giá tiền cọc
    /// </summary>
    Task<IEnumerable<DepositInfo>> GetDepositHistoryAsync();
}

/// <summary>
/// DTO cho thông tin Deposit
/// </summary>
public class DepositInfo
{
    public int DepositId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

