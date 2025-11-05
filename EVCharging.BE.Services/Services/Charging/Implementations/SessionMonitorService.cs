using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    /// <summary>
    /// Service theo dõi tiến trình phiên sạc real-time
    /// </summary>
    public class SessionMonitorService : ISessionMonitorService
    {
        private readonly IDbContextFactory<EvchargingManagementContext> _dbFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<int, Timer> _monitoringTimers = new(); // ✅ Thread-safe cho Singleton
        private readonly ConcurrentDictionary<int, ChargingSessionResponse> _activeSessions = new(); // ✅ Thread-safe cho Singleton

        public SessionMonitorService(IDbContextFactory<EvchargingManagementContext> dbFactory, IServiceProvider serviceProvider)
        {
            _dbFactory = dbFactory;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Bắt đầu theo dõi phiên sạc
        /// </summary>
        public async Task StartMonitoringAsync(int sessionId)
        {
            try
            {
                // ✅ Thread-safe: Kiểm tra xem đã có timer chưa
                if (_monitoringTimers.ContainsKey(sessionId))
                    return; // Already monitoring

                // Create timer to check session every 1 minutes
                var timer = new Timer(async _ => await MonitorSessionAsync(sessionId), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
                
                // ✅ Thread-safe: TryAdd sẽ trả về false nếu key đã tồn tại (race condition protection)
                if (!_monitoringTimers.TryAdd(sessionId, timer))
                {
                    // Nếu có race condition (timer đã được thêm bởi thread khác), dispose timer mới tạo
                    timer.Dispose();
                    return; // Already monitoring
                }

                Console.WriteLine($"Started monitoring session {sessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting monitoring for session {sessionId}: {ex.Message}");
                // Remove from dictionary nếu có lỗi
                _monitoringTimers.TryRemove(sessionId, out _);
            }
        }

        /// <summary>
        /// Dừng theo dõi phiên sạc
        /// </summary>
        public async Task StopMonitoringAsync(int sessionId)
        {
            try
            {
                if (_monitoringTimers.TryGetValue(sessionId, out var timer))
                {
                    timer.Dispose();
                    // ✅ Thread-safe: ConcurrentDictionary dùng TryRemove thay vì Remove
                    _monitoringTimers.TryRemove(sessionId, out _);
                }

                // ✅ Thread-safe: ConcurrentDictionary dùng TryRemove thay vì Remove
                _activeSessions.TryRemove(sessionId, out _);
                Console.WriteLine($"Stopped monitoring session {sessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping monitoring for session {sessionId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Kiểm tra phiên sạc có đang hoạt động không
        /// </summary>
        public async Task<bool> IsSessionActiveAsync(int sessionId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var session = await db.ChargingSessions.FindAsync(sessionId);
                return session?.Status == "in_progress";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy trạng thái phiên sạc
        /// </summary>
        public async Task<ChargingSessionResponse?> GetSessionStatusAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var chargingService = scope.ServiceProvider.GetRequiredService<IChargingService>();
                return await chargingService.GetSessionByIdAsync(sessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting session status: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cập nhật dữ liệu phiên sạc
        /// </summary>
        public async Task UpdateSessionDataAsync(int sessionId, int soc, decimal power, decimal voltage, decimal temperature)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var chargingService = scope.ServiceProvider.GetRequiredService<IChargingService>();
                
                var success = await chargingService.UpdateSessionProgressAsync(sessionId, soc, power, voltage, temperature);
                
                if (success)
                {
                    // Update cached session data
                    var sessionData = await chargingService.GetSessionByIdAsync(sessionId);
                    if (sessionData != null)
                    {
                        _activeSessions[sessionId] = sessionData;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating session data: {ex.Message}");
            }
        }

        /// <summary>
        /// Thông báo cập nhật phiên sạc
        /// </summary>
        public async Task NotifySessionUpdateAsync(int sessionId, ChargingSessionResponse sessionData)
        {
            try
            {
                // TODO: Implement SignalR notification in API layer
                Console.WriteLine($"Session {sessionId} updated: SOC={sessionData.CurrentSOC}%, Power={sessionData.CurrentPower}kW");
                
                // Update cached data
                _activeSessions[sessionId] = sessionData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error notifying session update: {ex.Message}");
            }
        }

        /// <summary>
        /// Kiểm tra cảnh báo phiên sạc
        /// </summary>
        public async Task CheckSessionAlertsAsync(int sessionId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                
                var session = await db.ChargingSessions
                    .Include(s => s.Point)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Status != "in_progress")
                    return;

                var alerts = new List<string>();

                // Check for high temperature
                var latestLog = await db.SessionLogs
                    .Where(sl => sl.SessionId == sessionId)
                    .OrderByDescending(sl => sl.LogTime)
                    .FirstOrDefaultAsync();

                if (latestLog?.Temperature > 60) // 60°C threshold
                {
                    alerts.Add($"High temperature detected: {latestLog.Temperature}°C");
                }

                // Check for low power output
                if (latestLog?.CurrentPower < 1.0m) // Less than 1kW
                {
                    alerts.Add($"Low power output: {latestLog.CurrentPower}kW");
                }

                // Check for long session duration
                var duration = DateTime.UtcNow - session.StartTime;
                if (duration.TotalHours > 8) // 8 hours threshold
                {
                    alerts.Add($"Long session duration: {duration.TotalHours:F1} hours");
                }

                // Send alerts
                foreach (var alert in alerts)
                {
                    await SendSessionErrorNotificationAsync(sessionId, alert);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking session alerts: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi thông báo hoàn thành phiên sạc
        /// </summary>
        public async Task SendSessionCompleteNotificationAsync(int sessionId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                
                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                    return;

                var message = $"Charging session completed! " +
                             $"Energy: {session.EnergyUsed:F2} kWh, " +
                             $"Cost: {session.FinalCost:F0} VND, " +
                             $"Duration: {session.DurationMinutes} minutes";

                Console.WriteLine($"Session {sessionId} completed: {message}");
                
                // TODO: Send notification to user
                // await _notificationService.SendAsync(session.Driver.UserId, "Charging Complete", message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending completion notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi thông báo lỗi phiên sạc
        /// </summary>
        public async Task SendSessionErrorNotificationAsync(int sessionId, string errorMessage)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                
                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                    return;

                Console.WriteLine($"Session {sessionId} error: {errorMessage}");
                
                // TODO: Send error notification to user and admin
                // await _notificationService.SendAsync(session.Driver.UserId, "Charging Alert", errorMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending error notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy analytics phiên sạc
        /// </summary>
        public async Task<Dictionary<string, object>> GetSessionAnalyticsAsync(int sessionId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                
                var session = await db.ChargingSessions
                    .Include(s => s.SessionLogs)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                    return new Dictionary<string, object>();

                var logs = session.SessionLogs.OrderBy(sl => sl.LogTime).ToList();
                
                var analytics = new Dictionary<string, object>
                {
                    ["sessionId"] = sessionId,
                    ["totalEnergy"] = session.EnergyUsed ?? 0,
                    ["totalCost"] = session.FinalCost ?? 0,
                    ["duration"] = session.DurationMinutes ?? 0,
                    ["averagePower"] = logs.Any() ? logs.Average(l => l.CurrentPower ?? 0) : 0,
                    ["maxPower"] = logs.Any() ? logs.Max(l => l.CurrentPower ?? 0) : 0,
                    ["averageTemperature"] = logs.Any() ? logs.Average(l => l.Temperature ?? 0) : 0,
                    ["maxTemperature"] = logs.Any() ? logs.Max(l => l.Temperature ?? 0) : 0,
                    ["socIncrease"] = (session.FinalSoc ?? session.InitialSoc) - session.InitialSoc,
                    ["efficiency"] = await CalculateEfficiencyAsync(sessionId)
                };

                return analytics;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting session analytics: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Tính hiệu suất sạc
        /// </summary>
        public async Task<decimal> CalculateEfficiencyAsync(int sessionId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                
                var session = await db.ChargingSessions
                    .Include(s => s.Point)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Point == null)
                    return 0;

                var theoreticalEnergy = (decimal)(session.Point.PowerOutput * (double)((session.DurationMinutes ?? 0) / 60.0));
                var actualEnergy = session.EnergyUsed;
                
                if (theoreticalEnergy > 0)
                    return (actualEnergy ?? 0) / theoreticalEnergy * 100;
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating efficiency: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Ước tính thời gian còn lại
        /// </summary>
        public async Task<TimeSpan> EstimateRemainingTimeAsync(int sessionId, int targetSOC)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                
                var session = await db.ChargingSessions
                    .Include(s => s.Point)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Point == null)
                    return TimeSpan.Zero;

                var currentSOC = await GetCurrentSOCAsync(sessionId);
                var socNeeded = targetSOC - currentSOC;
                
                if (socNeeded <= 0)
                    return TimeSpan.Zero;

                // Estimate based on current power and battery capacity
                var logs = await db.SessionLogs
                    .Where(sl => sl.SessionId == sessionId)
                    .OrderByDescending(sl => sl.LogTime)
                    .Take(5)
                    .ToListAsync();

                var averagePower = logs.Any() ? logs.Average(l => l.CurrentPower ?? 0) : (decimal)session.Point.PowerOutput;
                
                // Assume 75kWh battery capacity (can be made dynamic)
                var batteryCapacity = 75m;
                var energyNeeded = socNeeded / 100m * batteryCapacity;
                var hoursNeeded = energyNeeded / averagePower;
                
                return TimeSpan.FromHours((double)hoursNeeded);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error estimating remaining time: {ex.Message}");
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Lấy SOC hiện tại
        /// </summary>
        private async Task<int> GetCurrentSOCAsync(int sessionId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                
                var latestLog = await db.SessionLogs
                    .Where(sl => sl.SessionId == sessionId)
                    .OrderByDescending(sl => sl.LogTime)
                    .FirstOrDefaultAsync();

                return latestLog?.SocPercentage ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Monitor session (called by timer)
        /// </summary>
        private async Task MonitorSessionAsync(int sessionId)
        {
            try
            {
                if (!await IsSessionActiveAsync(sessionId))
                {
                    await StopMonitoringAsync(sessionId);
                    return;
                }

                // Check for alerts
                await CheckSessionAlertsAsync(sessionId);

                // Update session data if needed
                var sessionData = await GetSessionStatusAsync(sessionId);
                if (sessionData != null)
                {
                    await NotifySessionUpdateAsync(sessionId, sessionData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error monitoring session {sessionId}: {ex.Message}");
            }
        }
    }
}
