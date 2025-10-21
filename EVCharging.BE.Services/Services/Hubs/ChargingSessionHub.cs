using Microsoft.AspNetCore.SignalR;

namespace EVCharging.BE.API.Hubs
{
    /// <summary>
    /// SignalR Hub cho real-time updates của charging sessions
    /// </summary>
    public class ChargingSessionHub : Hub
    {
        /// <summary>
        /// Kết nối client vào group theo session ID
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        public async Task JoinSessionGroup(int sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Session_{sessionId}");
            await Clients.Caller.SendAsync("JoinedSession", sessionId);
        }

        /// <summary>
        /// Rời khỏi group theo session ID
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        public async Task LeaveSessionGroup(int sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Session_{sessionId}");
            await Clients.Caller.SendAsync("LeftSession", sessionId);
        }

        /// <summary>
        /// Kết nối client vào group theo driver ID
        /// </summary>
        /// <param name="driverId">ID driver</param>
        public async Task JoinDriverGroup(int driverId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Driver_{driverId}");
            await Clients.Caller.SendAsync("JoinedDriver", driverId);
        }

        /// <summary>
        /// Rời khỏi group theo driver ID
        /// </summary>
        /// <param name="driverId">ID driver</param>
        public async Task LeaveDriverGroup(int driverId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Driver_{driverId}");
            await Clients.Caller.SendAsync("LeftDriver", driverId);
        }

        /// <summary>
        /// Kết nối client vào group theo station ID
        /// </summary>
        /// <param name="stationId">ID trạm</param>
        public async Task JoinStationGroup(int stationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Station_{stationId}");
            await Clients.Caller.SendAsync("JoinedStation", stationId);
        }

        /// <summary>
        /// Rời khỏi group theo station ID
        /// </summary>
        /// <param name="stationId">ID trạm</param>
        public async Task LeaveStationGroup(int stationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Station_{stationId}");
            await Clients.Caller.SendAsync("LeftStation", stationId);
        }

        /// <summary>
        /// Khi client kết nối
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Khi client ngắt kết nối
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Clients.Caller.SendAsync("Disconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
