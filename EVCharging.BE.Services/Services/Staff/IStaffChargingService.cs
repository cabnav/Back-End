using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Common.DTOs.Staff;

namespace EVCharging.BE.Services.Services.Staff
{
    /// <summary>
    /// Service quản lý phiên sạc từ góc độ Staff (Nhân viên trạm sạc)
    /// </summary>
    public interface IStaffChargingService
    {
        // ========== STATION ASSIGNMENT & VERIFICATION ==========
        
        /// <summary>
        /// Kiểm tra staff có được assigned vào trạm này không
        /// </summary>
        /// <param name="staffId">ID của staff</param>
        /// <param name="stationId">ID của trạm</param>
        /// <returns>True nếu staff được assigned và đang trong ca làm việc</returns>
        Task<bool> VerifyStaffAssignmentAsync(int staffId, int stationId);

        /// <summary>
        /// Lấy danh sách trạm mà staff đang được assigned (trong ca làm việc)
        /// </summary>
        /// <param name="staffId">ID của staff</param>
        /// <returns>Danh sách station IDs</returns>
        Task<List<int>> GetAssignedStationsAsync(int staffId);

        /// <summary>
        /// Lấy thông tin trạm được assigned (bao gồm thống kê)
        /// </summary>
        /// <param name="staffId">ID của staff</param>
        /// <returns>Thông tin trạm</returns>
        Task<StationInfo?> GetMyStationInfoAsync(int staffId);

        // ========== WALK-IN CUSTOMER SESSION MANAGEMENT ==========

        /// <summary>
        /// Khởi động phiên sạc cho khách walk-in (không có app)
        /// </summary>
        /// <param name="staffId">ID của staff khởi động</param>
        /// <param name="request">Thông tin khởi động phiên sạc</param>
        /// <returns>Thông tin phiên sạc đã tạo</returns>
        Task<WalkInSessionResponse?> StartWalkInSessionAsync(int staffId, WalkInSessionRequest request);

        // ========== EMERGENCY OPERATIONS ==========

        /// <summary>
        /// Dừng khẩn cấp phiên sạc
        /// </summary>
        /// <param name="staffId">ID của staff thực hiện</param>
        /// <param name="sessionId">ID của phiên sạc</param>
        /// <param name="request">Thông tin dừng khẩn cấp</param>
        /// <returns>Thông tin phiên sạc đã dừng</returns>
        Task<ChargingSessionResponse?> EmergencyStopSessionAsync(int staffId, int sessionId, EmergencyStopRequest request);

        // ========== PAUSE/RESUME OPERATIONS ==========

        /// <summary>
        /// Tạm dừng phiên sạc
        /// </summary>
        /// <param name="staffId">ID của staff thực hiện</param>
        /// <param name="sessionId">ID của phiên sạc</param>
        /// <param name="request">Thông tin tạm dừng</param>
        /// <returns>Thông tin phiên sạc đã tạm dừng</returns>
        Task<ChargingSessionResponse?> PauseSessionAsync(int staffId, int sessionId, PauseSessionRequest request);

        /// <summary>
        /// Tiếp tục phiên sạc đã tạm dừng
        /// </summary>
        /// <param name="staffId">ID của staff thực hiện</param>
        /// <param name="sessionId">ID của phiên sạc</param>
        /// <param name="request">Thông tin tiếp tục</param>
        /// <returns>Thông tin phiên sạc đã tiếp tục</returns>
        Task<ChargingSessionResponse?> ResumeSessionAsync(int staffId, int sessionId, ResumeSessionRequest request);

        // ========== SESSION MONITORING ==========

        /// <summary>
        /// Lấy danh sách phiên sạc tại trạm của staff
        /// </summary>
        /// <param name="staffId">ID của staff</param>
        /// <param name="filter">Bộ lọc</param>
        /// <returns>Dashboard với danh sách phiên sạc</returns>
        Task<StaffSessionsDashboard> GetMyStationSessionsAsync(int staffId, StaffSessionsFilterRequest filter);

        /// <summary>
        /// Lấy chi tiết phiên sạc (chỉ nếu thuộc trạm của staff)
        /// </summary>
        /// <param name="staffId">ID của staff</param>
        /// <param name="sessionId">ID của phiên sạc</param>
        /// <returns>Thông tin phiên sạc</returns>
        Task<ChargingSessionResponse?> GetSessionDetailAsync(int staffId, int sessionId);

        // ========== LOGGING & AUDIT ==========

        /// <summary>
        /// Log hành động của staff
        /// </summary>
        /// <param name="staffId">ID của staff</param>
        /// <param name="action">Hành động thực hiện</param>
        /// <param name="sessionId">ID phiên sạc liên quan</param>
        /// <param name="details">Chi tiết bổ sung</param>
        Task LogStaffActionAsync(int staffId, string action, int sessionId, string? details = null);
    }
}

