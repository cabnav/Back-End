using EVCharging.BE.Common.DTOs.Charging;

namespace EVCharging.BE.Services.Services.Notification
{
    /// <summary>
    /// Service để gửi thông báo real-time qua SignalR
    /// </summary>
    public interface ISignalRNotificationService
    {
        Task NotifySessionUpdateAsync(int sessionId, ChargingSessionResponse sessionData);
        Task NotifySessionCompletedAsync(int sessionId, ChargingSessionResponse sessionData);
        Task NotifySessionErrorAsync(int sessionId, string errorMessage);
        Task NotifyPriceChangedAsync(int chargingPointId, decimal oldPrice, decimal newPrice);
    }
}
