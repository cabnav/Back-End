using EVCharging.BE.Common.DTOs.Charging;

namespace EVCharging.BE.Services.Services.Charging
{
    /// <summary>
    /// Interface cho Session Monitor Service - theo dõi tiến trình sạc
    /// </summary>
    public interface ISessionMonitorService
    {
        // Monitoring
        Task StartMonitoringAsync(int sessionId);
        Task StopMonitoringAsync(int sessionId);
        Task<bool> IsSessionActiveAsync(int sessionId);
        
        // Real-time Data
        Task<ChargingSessionResponse?> GetSessionStatusAsync(int sessionId);
        Task UpdateSessionDataAsync(int sessionId, int soc, decimal power, decimal voltage, decimal temperature);
        Task NotifySessionUpdateAsync(int sessionId, ChargingSessionResponse sessionData);
        
        // Alerts & Notifications
        Task CheckSessionAlertsAsync(int sessionId);
        Task SendSessionCompleteNotificationAsync(int sessionId);
        Task SendSessionErrorNotificationAsync(int sessionId, string errorMessage);
        
        // Analytics
        Task<Dictionary<string, object>> GetSessionAnalyticsAsync(int sessionId);
        Task<decimal> CalculateEfficiencyAsync(int sessionId);
        Task<TimeSpan> EstimateRemainingTimeAsync(int sessionId, int targetSOC);
        
        // Monitoring Status
        Task<Dictionary<string, object>> GetMonitoringStatusAsync(int sessionId);
    }
}
