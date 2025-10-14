using EVCharging.BE.Common.DTOs.Charging;

namespace EVCharging.BE.Services.Services
{
    /// <summary>
    /// Interface cho Charging Service - quản lý phiên sạc
    /// </summary>
    public interface IChargingService
    {
        // Session Management
        Task<ChargingSessionResponse?> StartSessionAsync(ChargingSessionStartRequest request);
        Task<ChargingSessionResponse?> StopSessionAsync(ChargingSessionStopRequest request);
        Task<ChargingSessionResponse?> UpdateSessionStatusAsync(ChargingSessionStatusRequest request);
        Task<ChargingSessionResponse?> GetSessionByIdAsync(int sessionId);
        Task<IEnumerable<ChargingSessionResponse>> GetActiveSessionsAsync();
        Task<IEnumerable<ChargingSessionResponse>> GetSessionsByDriverAsync(int driverId);
        Task<IEnumerable<ChargingSessionResponse>> GetSessionsByStationAsync(int stationId);

        // Session Logs
        Task<bool> CreateSessionLogAsync(SessionLogCreateRequest request);
        Task<IEnumerable<SessionLogDTO>> GetSessionLogsAsync(int sessionId);
        Task<bool> UpdateSessionProgressAsync(int sessionId, int soc, decimal power, decimal voltage, decimal temperature);

        // Validation
        Task<bool> ValidateChargingPointAsync(int chargingPointId);
        Task<bool> ValidateDriverAsync(int driverId);
        Task<bool> CanStartSessionAsync(int chargingPointId, int driverId);
    }
}
