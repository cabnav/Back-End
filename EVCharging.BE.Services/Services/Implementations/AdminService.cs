using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly EvchargingManagementContext _db;

        public AdminService(EvchargingManagementContext db)
        {
            _db = db;
        }

        // ✅ 1. Thống kê tổng quan hệ thống
        public async Task<object> GetSystemStatsAsync()
        {
            var totalUsers = await _db.Users.CountAsync();
            var totalStations = await _db.ChargingStations.CountAsync();
            var totalPoints = await _db.ChargingPoints.CountAsync();
            var activeSessions = await _db.ChargingSessions.CountAsync(s => s.Status == "active");
            var totalRevenue = await _db.Payments.SumAsync(p => (decimal?)p.Amount) ?? 0;

            return new
            {
                TotalUsers = totalUsers,
                TotalStations = totalStations,
                TotalPoints = totalPoints,
                ActiveSessions = activeSessions,
                TotalRevenue = Math.Round(totalRevenue, 2)
            };
        }

        // ✅ 2. Hiệu suất trạm sạc
        public async Task<object> GetStationPerformanceAsync()
        {
            var data = await _db.ChargingStations
                .Include(s => s.ChargingPoints)
                .Select(s => new
                {
                    s.StationId,
                    s.Name,
                    TotalPoints = s.ChargingPoints.Count,
                    Active = s.ChargingPoints.Count(p => p.Status == "Busy"),
                    Available = s.ChargingPoints.Count(p => p.Status == "Available"),
                    UtilizationRate = s.ChargingPoints.Count == 0
                        ? 0
                        : Math.Round(
                            (double)s.ChargingPoints.Count(p => p.Status == "Busy") / s.ChargingPoints.Count * 100, 1)
                })
                .ToListAsync();

            return data;
        }

        // ✅ 3. Báo cáo doanh thu
        public async Task<object> GetRevenueAnalyticsAsync(DateTime? from = null, DateTime? to = null)
        {
            var startDate = from ?? DateTime.Now.AddDays(-30);
            var endDate = to ?? DateTime.Now;

            var dailyRevenue = await _db.Payments
                .Where(p => p.CreatedAt.HasValue &&
                            p.CreatedAt.Value >= startDate &&
                            p.CreatedAt.Value <= endDate)
                .GroupBy(p => p.CreatedAt.Value.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(p => p.Amount)
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            var total = dailyRevenue.Sum(x => x.Total);

            return new
            {
                Period = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}",
                TotalRevenue = total,
                Daily = dailyRevenue
            };
        }

        // ✅ 4. Thống kê xu hướng sử dụng
        public async Task<object> GetUsagePatternAsync()
        {
            var usageByHour = await _db.ChargingSessions
                .GroupBy(s => s.StartTime.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .OrderBy(g => g.Hour)
                .ToListAsync();

            var usageByDay = await _db.ChargingSessions
                .GroupBy(s => s.StartTime.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            return new
            {
                ByHour = usageByHour,
                ByDay = usageByDay
            };
        }

        // ✅ 5. Hiệu suất nhân viên
        public async Task<object> GetStaffPerformanceAsync()
        {
            var staffData = await _db.Users
                .Where(u => u.Role == "staff")
                .Select(u => new
                {
                    u.UserId,
                    u.Name,
                    SessionsHandled = _db.ChargingSessions.Count(s => s.StaffId == u.UserId),
                    RevenueHandled = _db.Payments
                        .Where(p => p.StaffId == u.UserId)
                        .Sum(p => (decimal?)p.Amount) ?? 0
                })
                .ToListAsync();

            return staffData;
        }
    }
}
