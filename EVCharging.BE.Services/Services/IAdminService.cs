using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services
{
    public interface IAdminService
    {
        Task<object> GetSystemStatsAsync();
        Task<object> GetStationPerformanceAsync();
        Task<object> GetRevenueAnalyticsAsync(DateTime? from = null, DateTime? to = null);
        Task<object> GetUsagePatternAsync();
        Task<object> GetStaffPerformanceAsync();
    }
}
