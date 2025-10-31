using EVCharging.BE.Common.DTOs.Charging;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Dashboard tổng quan về phiên sạc tại trạm của staff
    /// </summary>
    public class StaffSessionsDashboard
    {
        /// <summary>
        /// Thông tin trạm được assigned
        /// </summary>
        public StationInfo Station { get; set; } = new();

        /// <summary>
        /// Danh sách phiên sạc
        /// </summary>
        public List<ChargingSessionResponse> Sessions { get; set; } = new();

        /// <summary>
        /// Tổng số phiên sạc
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Trang hiện tại
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Tổng số trang
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Thống kê nhanh
        /// </summary>
        public QuickStats Stats { get; set; } = new();
    }

    public class StationInfo
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int TotalPoints { get; set; }
        public int AvailablePoints { get; set; }
        public int InUsePoints { get; set; }
        public int MaintenancePoints { get; set; }
        public int OfflinePoints { get; set; }
    }

    public class QuickStats
    {
        /// <summary>
        /// Số phiên sạc đang hoạt động
        /// </summary>
        public int ActiveSessions { get; set; }

        /// <summary>
        /// Số phiên sạc đã hoàn thành hôm nay
        /// </summary>
        public int CompletedToday { get; set; }

        /// <summary>
        /// Số phiên walk-in hôm nay
        /// </summary>
        public int WalkInToday { get; set; }

        /// <summary>
        /// Tổng doanh thu ước tính hôm nay
        /// </summary>
        public decimal EstimatedRevenueToday { get; set; }

        /// <summary>
        /// Số sự cố hôm nay
        /// </summary>
        public int IncidentsToday { get; set; }
    }
}

