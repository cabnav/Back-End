namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Filter để lấy danh sách staff assignments
    /// </summary>
    public class StaffAssignmentFilterRequest
    {
        /// <summary>
        /// Lọc theo staff ID
        /// </summary>
        public int? StaffId { get; set; }

        /// <summary>
        /// Lọc theo station ID
        /// </summary>
        public int? StationId { get; set; }

        /// <summary>
        /// Lọc theo status: "active", "inactive", "all"
        /// </summary>
        public string Status { get; set; } = "all";

        /// <summary>
        /// Chỉ lấy assignments đang active (trong thời gian shift)
        /// </summary>
        public bool? OnlyActiveShifts { get; set; }

        /// <summary>
        /// Lọc theo ngày (chỉ lấy shifts trong ngày này)
        /// </summary>
        public DateTime? Date { get; set; }

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




