using EVCharging.BE.Common.DTOs.Charging;

namespace EVCharging.BE.Services.Services.Charging
{
    /// <summary>
    /// Interface cho Charging Service - quản lý phiên sạc
    /// </summary>
    public interface IChargingService
    {
        // ========== SESSION MANAGEMENT ==========
        
        /// <summary>
        /// Bắt đầu phiên sạc mới
        /// </summary>
        Task<ChargingSessionResponse?> StartSessionAsync(ChargingSessionStartRequest request);
        
        /// <summary>
        /// Dừng phiên sạc và tính toán chi phí
        /// </summary>
        Task<ChargingSessionResponse?> StopSessionAsync(ChargingSessionStopRequest request);
        
        /// <summary>
        /// Cập nhật trạng thái phiên sạc (chủ yếu dùng bởi Staff service)
        /// </summary>
        Task<ChargingSessionResponse?> UpdateSessionStatusAsync(ChargingSessionStatusRequest request);
        
        /// <summary>
        /// Lấy thông tin phiên sạc theo ID
        /// </summary>
        Task<ChargingSessionResponse?> GetSessionByIdAsync(int sessionId);
        
        /// <summary>
        /// Lấy danh sách phiên sạc đang hoạt động
        /// </summary>
        Task<IEnumerable<ChargingSessionResponse>> GetActiveSessionsAsync();
        
        /// <summary>
        /// Lấy danh sách phiên sạc theo driver
        /// </summary>
        Task<IEnumerable<ChargingSessionResponse>> GetSessionsByDriverAsync(int driverId);
        
        /// <summary>
        /// Lấy danh sách phiên sạc theo trạm
        /// </summary>
        Task<IEnumerable<ChargingSessionResponse>> GetSessionsByStationAsync(int stationId);

        // ========== SESSION LOGS ==========
        
        /// <summary>
        /// Tạo log cho phiên sạc (chủ yếu dùng bởi Staff service)
        /// </summary>
        Task<bool> CreateSessionLogAsync(SessionLogCreateRequest request);
        
        /// <summary>
        /// Lấy logs của phiên sạc
        /// </summary>
        Task<IEnumerable<SessionLogDTO>> GetSessionLogsAsync(int sessionId);
        
        /// <summary>
        /// Cập nhật tiến trình phiên sạc (tự động tạo log) - dùng bởi SessionMonitorService
        /// </summary>
        Task<bool> UpdateSessionProgressAsync(int sessionId, int soc, decimal power, decimal voltage, decimal temperature);

        // ========== VALIDATION ==========
        
        /// <summary>
        /// Validate charging point availability
        /// </summary>
        Task<bool> ValidateChargingPointAsync(int chargingPointId);
        
        /// <summary>
        /// Validate driver exists and has user account
        /// </summary>
        Task<bool> ValidateDriverAsync(int driverId);
        
        /// <summary>
        /// Kiểm tra có thể bắt đầu phiên sạc không (driver không có session active, point available)
        /// </summary>
        Task<bool> CanStartSessionAsync(int chargingPointId, int driverId);
    }
}
