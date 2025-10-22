using EVCharging.BE.Common.DTOs.Charging;

namespace EVCharging.BE.Services.Services.Charging
{
    /// <summary>
    /// Interface for real-time charging session monitoring
    /// </summary>
    public interface IRealTimeChargingService
    {
        /// <summary>
        /// Get real-time session data including SOC and remaining time
        /// </summary>
        Task<RealTimeSessionDTO?> GetRealTimeSessionAsync(int sessionId);
        
        /// <summary>
        /// Update session with current SOC and power data
        /// </summary>
        Task<bool> UpdateSessionDataAsync(int sessionId, int currentSOC, double currentPower);
        
        /// <summary>
        /// Calculate estimated remaining time based on current SOC and target
        /// </summary>
        Task<int?> CalculateRemainingTimeAsync(int sessionId, int currentSOC, int? targetSOC);
        
        /// <summary>
        /// Get all active sessions for a driver
        /// </summary>
        Task<IEnumerable<RealTimeSessionDTO>> GetActiveSessionsAsync(int driverId);
        
        /// <summary>
        /// Check if charging is complete and send notifications
        /// </summary>
        Task<bool> CheckChargingCompletionAsync(int sessionId);
    }
}
