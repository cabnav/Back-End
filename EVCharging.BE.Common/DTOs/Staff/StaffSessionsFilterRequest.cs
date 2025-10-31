namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Filter để lấy danh sách phiên sạc tại trạm của staff
    /// </summary>
    public class StaffSessionsFilterRequest
    {
        /// <summary>
        /// Lọc theo trạng thái: all, active, completed, paused, cancelled
        /// </summary>
        public string Status { get; set; } = "all";

        /// <summary>
        /// Lọc theo ngày (nullable, default = hôm nay)
        /// </summary>
        public DateTime? Date { get; set; }

        /// <summary>
        /// Từ ngày (nullable, để lấy range)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Đến ngày (nullable, để lấy range)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Chỉ lấy walk-in sessions
        /// </summary>
        public bool? OnlyWalkIn { get; set; }

        /// <summary>
        /// Trang hiện tại
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Số items mỗi trang
        /// </summary>
        public int PageSize { get; set; } = 20;
    }
}

