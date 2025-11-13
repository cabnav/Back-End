using EVCharging.BE.Common.DTOs.Shared;
using EVCharging.BE.Common.DTOs.Staff;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Admin
{
    public interface IAdminService
    {
        Task<object> GetSystemStatsAsync();
        Task<object> GetStationPerformanceAsync();
        Task<object> GetRevenueAnalyticsAsync(DateTime? from = null, DateTime? to = null);
        Task<object> GetUsagePatternAsync();
        Task<object> GetStaffPerformanceAsync();
        Task<object> GetRevenueByStationAndMethodAsync();

        // ========== INCIDENT REPORT MANAGEMENT ==========

        /// <summary>
        /// Lấy danh sách báo cáo sự cố (cho admin)
        /// </summary>
        Task<object> GetIncidentReportsAsync(IncidentReportFilter filter);

        /// <summary>
        /// Lấy chi tiết báo cáo sự cố
        /// </summary>
        Task<IncidentReportResponse?> GetIncidentReportByIdAsync(int reportId);

        /// <summary>
        /// Cập nhật trạng thái báo cáo sự cố
        /// </summary>
        Task<IncidentReportResponse?> UpdateIncidentReportStatusAsync(int reportId, int adminId, UpdateIncidentStatusRequest request);
    }
}
