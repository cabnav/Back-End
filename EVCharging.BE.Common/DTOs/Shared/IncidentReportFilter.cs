namespace EVCharging.BE.Common.DTOs.Shared
{
    /// <summary>
    /// Filter cho danh sách báo cáo sự cố
    /// </summary>
    public class IncidentReportFilter
    {
        /// <summary>
        /// Lọc theo trạng thái (all, open, in_progress, resolved)
        /// </summary>
        public string Status { get; set; } = "all";

        /// <summary>
        /// Lọc theo mức độ ưu tiên (all, low, medium, high, critical)
        /// </summary>
        public string Priority { get; set; } = "all";

        /// <summary>
        /// Lọc theo trạm sạc (0 = tất cả)
        /// </summary>
        public int? StationId { get; set; }

        /// <summary>
        /// Lọc theo người báo cáo (0 = tất cả)
        /// </summary>
        public int? ReporterId { get; set; }

        /// <summary>
        /// Từ ngày
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Đến ngày
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Số trang (mặc định 1)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Số bản ghi mỗi trang (mặc định 20)
        /// </summary>
        public int PageSize { get; set; } = 20;
    }
}

