using EVCharging.BE.Common.DTOs.Analytics;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services
{
    public class AnalyticsService
    {
        private readonly EvchargingManagementContext _context;

        public AnalyticsService(EvchargingManagementContext context)
        {
            _context = context;
        }

        public async Task<DriverMonthlyReportDto> GetDriverMonthlyReportAsync(int driverId, int year, int month)
        {
            try
            {
                // ✅ Bước 1: Lấy toàn bộ session theo driver và status
                var allSessions = await _context.ChargingSessions
                    .Where(s => s.DriverId == driverId && s.Status.ToLower() == "completed")
                    .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                    .ToListAsync();

                // ✅ Bước 2: Lọc theo thời gian ở phía ứng dụng (không trong SQL)
                var sessions = allSessions
                    .Where(s =>
                    {
                        var localTime = s.StartTime.Kind == DateTimeKind.Utc
                            ? s.StartTime.ToLocalTime()
                            : s.StartTime;
                        return localTime.Year == year && localTime.Month == month;
                    })
                    .ToList();

                if (!sessions.Any())
                {
                    return new DriverMonthlyReportDto
                    {
                        TotalSessions = 0,
                        TotalEnergyUsed = 0,
                        FavoriteStation = "N/A",
                        PeakHour = "N/A",
                        AverageEnergyPerSession = 0,
                        TotalCost = 0
                    };
                }

                // ✅ Tính toán các giá trị
                decimal totalEnergy = sessions.Sum(s => s.EnergyUsed ?? 0);
                decimal totalCost = sessions.Sum(s => s.FinalCost ?? 0);

                var favoriteStation = sessions
                    .Where(s => s.Point?.Station != null)
                    .GroupBy(s => s.Point.Station.StationId)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().Point.Station.Name)
                    .FirstOrDefault() ?? "N/A";

                var peakHour = sessions
                    .GroupBy(s => s.StartTime.Hour)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault();

                return new DriverMonthlyReportDto
                {
                    TotalSessions = sessions.Count,
                    TotalEnergyUsed = Math.Round(totalEnergy, 2),
                    FavoriteStation = favoriteStation,
                    PeakHour = $"{peakHour}:00 - {peakHour + 1}:00",
                    AverageEnergyPerSession = Math.Round(totalEnergy / sessions.Count, 2),
                    TotalCost = Math.Round(totalCost, 2)
                };
            }
            catch (Exception ex)
            {
                // ✅ Xử lý exception và trả về HTTP 500
                throw new Exception($"An error occurred while retrieving active sessions: {ex.Message}", ex);
            }
        }
    }
}
