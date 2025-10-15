using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EVCharging.BE.API.Services
{
    /// <summary>
    /// Service implementation để gửi thông báo real-time qua SignalR
    /// </summary>
    public class SignalRNotificationService : ISignalRNotificationService
    {
        private readonly IHubContext<ChargingSessionHub> _hubContext;

        public SignalRNotificationService(IHubContext<ChargingSessionHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Thông báo cập nhật phiên sạc
        /// </summary>
        public async Task NotifySessionUpdateAsync(int sessionId, ChargingSessionResponse sessionData)
        {
            try
            {
                // Send to session group
                await _hubContext.Clients.Group($"Session_{sessionId}").SendAsync("SessionUpdated", sessionData);
                
                // Send to driver group
                await _hubContext.Clients.Group($"Driver_{sessionData.DriverId}").SendAsync("SessionUpdated", sessionData);
                
                // Send to station group
                await _hubContext.Clients.Group($"Station_{sessionData.ChargingPoint.StationId}").SendAsync("SessionUpdated", sessionData);
                
                Console.WriteLine($"SignalR: Session {sessionId} updated notification sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending session update notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Thông báo hoàn thành phiên sạc
        /// </summary>
        public async Task NotifySessionCompletedAsync(int sessionId, ChargingSessionResponse sessionData)
        {
            try
            {
                await _hubContext.Clients.Group($"Session_{sessionId}").SendAsync("SessionCompleted", sessionData);
                await _hubContext.Clients.Group($"Driver_{sessionData.DriverId}").SendAsync("SessionCompleted", sessionData);
                await _hubContext.Clients.Group($"Station_{sessionData.ChargingPoint.StationId}").SendAsync("SessionCompleted", sessionData);
                
                Console.WriteLine($"SignalR: Session {sessionId} completed notification sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending session completed notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Thông báo lỗi phiên sạc
        /// </summary>
        public async Task NotifySessionErrorAsync(int sessionId, string errorMessage)
        {
            try
            {
                var errorData = new { sessionId, errorMessage, timestamp = DateTime.UtcNow };
                
                await _hubContext.Clients.Group($"Session_{sessionId}").SendAsync("SessionError", errorData);
                
                Console.WriteLine($"SignalR: Session {sessionId} error notification sent: {errorMessage}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending session error notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Thông báo thay đổi giá
        /// </summary>
        public async Task NotifyPriceChangedAsync(int chargingPointId, decimal oldPrice, decimal newPrice)
        {
            try
            {
                var priceData = new { chargingPointId, oldPrice, newPrice, timestamp = DateTime.UtcNow };
                
                await _hubContext.Clients.Group($"Station_{chargingPointId}").SendAsync("PriceChanged", priceData);
                await _hubContext.Clients.All.SendAsync("PriceChanged", priceData);
                
                Console.WriteLine($"SignalR: Price change notification sent for charging point {chargingPointId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending price change notification: {ex.Message}");
            }
        }
    }
}
