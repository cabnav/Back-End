using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    /// <summary>
    /// Service theo dõi tiến trình phiên sạc real-time
    /// IMPORTANT: This service must be registered as Singleton to maintain state (timers)
    /// </summary>
    public class SessionMonitorService : ISessionMonitorService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SessionMonitorService> _logger;
        private readonly ConcurrentDictionary<int, Timer> _monitoringTimers = new();
        private readonly ConcurrentDictionary<int, bool> _monitoringInProgress = new(); // Prevent overlapping
        private readonly ConcurrentDictionary<int, ChargingSessionResponse> _activeSessions = new();
        private readonly ConcurrentDictionary<int, bool> _nearTargetSocNotified = new(); // Track if near target SOC notification was sent
        private readonly ConcurrentDictionary<int, bool> _reservationReminderNotified = new(); // Track if reservation reminder was sent
        private bool _disposed = false;

        public SessionMonitorService(IServiceProvider serviceProvider, ILogger<SessionMonitorService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Bắt đầu theo dõi phiên sạc
        /// </summary>
        public Task StartMonitoringAsync(int sessionId)
        {
            try
            {
                // ✅ Thread-safe: Kiểm tra xem đã có timer chưa
                if (_monitoringTimers.ContainsKey(sessionId))
                {
                    _logger.LogInformation("Session {SessionId} is already being monitored", sessionId);
                    return Task.CompletedTask;
                }

                // Create timer to check session every 1 minute
                // ✅ Delay first check by 2 minutes to avoid checking immediately after session start
                // This gives the session time to create logs and avoids premature auto-stop checks
                // Use Task.Run to properly handle async operations in timer callback
                var timer = new Timer(_ =>
                {
                    // Fire and forget - use Task.Run to avoid async void
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await MonitorSessionAsync(sessionId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in timer callback for session {SessionId}", sessionId);
                        }
                    });
                }, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1)); // ✅ First check after 2 minutes, then every 1 minute

                _monitoringTimers[sessionId] = timer;
                _logger.LogInformation("✅ [StartMonitoring] Started monitoring session {SessionId} - First check in 2 minutes, then every 1 minute", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [StartMonitoring] Error starting monitoring for session {SessionId}: {Error}", sessionId, ex.Message);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Dừng theo dõi phiên sạc
        /// </summary>
        public Task StopMonitoringAsync(int sessionId)
        {
            try
            {
                if (_monitoringTimers.TryRemove(sessionId, out var timer))
                {
                    timer?.Dispose();
                    _logger.LogInformation("Stopped monitoring session {SessionId}", sessionId);
                }

                _activeSessions.TryRemove(sessionId, out _);
                _monitoringInProgress.TryRemove(sessionId, out _);
                _nearTargetSocNotified.TryRemove(sessionId, out _);
                _reservationReminderNotified.TryRemove(sessionId, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping monitoring for session {SessionId}", sessionId);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Kiểm tra phiên sạc có đang hoạt động không
        /// </summary>
        public async Task<bool> IsSessionActiveAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

                var session = await db.ChargingSessions.FindAsync(sessionId);
                return session?.Status == "in_progress";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if session {SessionId} is active", sessionId);
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
                _logger.LogError(ex, "Error getting session status for session {SessionId}", sessionId);
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
                _logger.LogError(ex, "Error updating session data for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Thông báo cập nhật phiên sạc
        /// </summary>
        public Task NotifySessionUpdateAsync(int sessionId, ChargingSessionResponse sessionData)
        {
            try
            {
                // TODO: Implement SignalR notification in API layer
                _logger.LogInformation("Session {SessionId} updated: SOC={SOC}%, Power={Power}kW",
                    sessionId, sessionData.CurrentSOC, sessionData.CurrentPower);

                // Update cached data
                _activeSessions[sessionId] = sessionData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying session update for session {SessionId}", sessionId);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Kiểm tra cảnh báo phiên sạc
        /// </summary>
        public async Task CheckSessionAlertsAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

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
                    alerts.Add($"Nhiệt độ cao được phát hiện: {latestLog.Temperature:F1}°C. Vui lòng kiểm tra hệ thống sạc.");
                }

                // Check for low power output
                if (latestLog?.CurrentPower < 1.0m) // Less than 1kW
                {
                    alerts.Add($"Công suất sạc thấp: {latestLog.CurrentPower:F2} kW. Có thể có vấn đề với kết nối hoặc thiết bị.");
                }

                // Check for long session duration
                var duration = DateTime.UtcNow - session.StartTime;
                if (duration.TotalHours > 8) // 8 hours threshold
                {
                    alerts.Add($"Phiên sạc kéo dài: {duration.TotalHours:F1} giờ. Vui lòng kiểm tra pin và hệ thống sạc.");
                }

                // Send alerts
                foreach (var alert in alerts)
                {
                    await SendSessionErrorNotificationAsync(sessionId, alert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking session alerts for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Gửi thông báo hoàn thành phiên sạc
        /// </summary>
        public async Task SendSessionCompleteNotificationAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Driver?.User == null)
                    return;

                var userId = session.Driver.User.UserId;
                var stationName = session.Point?.Station?.Name ?? "trạm sạc";
                var finalSoc = session.FinalSoc ?? 100;
                var energyUsed = session.EnergyUsed ?? 0;
                var finalCost = session.FinalCost ?? 0;
                var durationMinutes = session.DurationMinutes ?? 0;

                var title = "Sạc đầy hoàn tất";
                var message = $"Phiên sạc của bạn đã hoàn tất tại {stationName}.\n" +
                             $"Pin đã sạc đến {finalSoc}%.\n" +
                             $"Năng lượng đã sạc: {energyUsed:F2} kWh\n" +
                             $"Thời gian sạc: {durationMinutes} phút\n" +
                             $"Chi phí: {finalCost:N0} VND";

                _logger.LogInformation("Session {SessionId} completed: {Message}", sessionId, message);

                await notificationService.SendNotificationAsync(
                    userId,
                    title,
                    message,
                    "charging_complete",
                    sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending completion notification for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Gửi thông báo lỗi phiên sạc
        /// </summary>
        public async Task SendSessionErrorNotificationAsync(int sessionId, string errorMessage)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Driver?.User == null)
                    return;

                var userId = session.Driver.User.UserId;
                var stationName = session.Point?.Station?.Name ?? "trạm sạc";

                var title = "Cảnh báo phiên sạc";
                var message = $"Phiên sạc tại {stationName} gặp vấn đề:\n{errorMessage}\n" +
                             $"Vui lòng kiểm tra hoặc liên hệ hỗ trợ nếu cần thiết.";

                _logger.LogWarning("Session {SessionId} error: {ErrorMessage}", sessionId, errorMessage);

                await notificationService.SendNotificationAsync(
                    userId,
                    title,
                    message,
                    "charging_alert",
                    sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending error notification for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Lấy analytics phiên sạc
        /// </summary>
        public async Task<Dictionary<string, object>> GetSessionAnalyticsAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

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
                    ["averagePower"] = logs.Any(l => l.CurrentPower.HasValue)
        ? logs.Where(l => l.CurrentPower.HasValue).Average(l => l.CurrentPower!.Value)
        : 0,
                    ["maxPower"] = logs.Any(l => l.CurrentPower.HasValue)
        ? logs.Where(l => l.CurrentPower.HasValue).Max(l => l.CurrentPower!.Value)
        : 0,
                    ["averageTemperature"] = logs.Any(l => l.Temperature.HasValue)
        ? logs.Where(l => l.Temperature.HasValue).Average(l => l.Temperature!.Value)
        : 0,
                    ["maxTemperature"] = logs.Any(l => l.Temperature.HasValue)
        ? logs.Where(l => l.Temperature.HasValue).Max(l => l.Temperature!.Value)
        : 0,
                    ["socIncrease"] = (session.FinalSoc ?? session.InitialSoc) - session.InitialSoc,
                    ["efficiency"] = await CalculateEfficiencyAsync(sessionId)
                };

                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session analytics for session {SessionId}", sessionId);
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
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

                var session = await db.ChargingSessions
                    .Include(s => s.Point)
                    .Include(s => s.SessionLogs)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Point == null)
                    return 0;

                // Tính theoretical energy từ PowerOutput và thời gian
                var powerOutput = session.Point.PowerOutput ?? 0;
                if (powerOutput == 0)
                    return 0;

                var durationHours = (session.DurationMinutes ?? 0) / 60.0;
                var theoreticalEnergy = (decimal)(powerOutput * durationHours);

                // Tính actual energy từ session (nếu có) hoặc từ logs
                var actualEnergy = session.EnergyUsed;

                // Nếu chưa có EnergyUsed, tính từ logs
                if (!actualEnergy.HasValue && session.SessionLogs != null && session.SessionLogs.Any())
                {
                    actualEnergy = CalculateEnergyUsedFromLogs(session);
                }

                if (theoreticalEnergy > 0 && actualEnergy.HasValue)
                    return (actualEnergy.Value / theoreticalEnergy) * 100;

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating efficiency for session {SessionId}", sessionId);
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
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

                var session = await db.ChargingSessions
                    .Include(s => s.Point)
                    .Include(s => s.Driver)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Point == null)
                    return TimeSpan.Zero;

                var currentSOC = await GetCurrentSOCAsync(sessionId);
                var socNeeded = targetSOC - currentSOC;

                if (socNeeded <= 0)
                    return TimeSpan.Zero;

                // Lấy averagePower từ logs hoặc PowerOutput
                var logs = await db.SessionLogs
                    .Where(sl => sl.SessionId == sessionId)
                    .OrderByDescending(sl => sl.LogTime)
                    .Take(5)
                    .ToListAsync();

                var averagePower = logs.Any() && logs.Any(l => l.CurrentPower.HasValue)
                    ? logs.Where(l => l.CurrentPower.HasValue).Average(l => l.CurrentPower!.Value)
                    : (decimal)(session.Point.PowerOutput ?? 50); // Fallback

                if (averagePower <= 0)
                    return TimeSpan.Zero;

                // Lấy battery capacity từ DriverProfile (không hardcode)
                var batteryCapacity = session.Driver?.BatteryCapacity ?? 75m; // Fallback nếu không có
                if (batteryCapacity <= 0)
                    return TimeSpan.Zero;

                var energyNeeded = (socNeeded / 100m) * batteryCapacity;
                var hoursNeeded = energyNeeded / averagePower;

                return TimeSpan.FromHours((double)hoursNeeded);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating remaining time for session {SessionId}", sessionId);
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Kiểm tra và tự động dừng session nếu đạt target SOC hoặc 100%
        /// </summary>
        private async Task<bool> CheckAndAutoStopSessionAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();
                var chargingService = scope.ServiceProvider.GetRequiredService<IChargingService>();

                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                    .Include(s => s.SessionLogs)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Status != "in_progress")
                    return false;

                // ✅ Tránh auto-stop ngay khi session vừa start (< 2 phút)
                // Session cần thời gian để sạc và tạo logs
                var sessionDuration = DateTime.UtcNow - session.StartTime;
                if (sessionDuration.TotalMinutes < 2)
                {
                    _logger.LogDebug("Session {SessionId} is too new ({Duration:F1} minutes), skipping auto-stop check",
                        sessionId, sessionDuration.TotalMinutes);
                    return false;
                }

                // ✅ Tính SOC hiện tại từ session đã load (không gọi GetCurrentSOCAsync để tránh duplicate query)
                int currentSOC;

                // Nếu có log, lấy từ log mới nhất
                var latestLog = session.SessionLogs?
                    .OrderByDescending(sl => sl.LogTime)
                    .FirstOrDefault();

                if (latestLog?.SocPercentage.HasValue == true)
                {
                    currentSOC = latestLog.SocPercentage.Value;
                }
                else if (session.Driver?.BatteryCapacity.HasValue == true &&
                         session.EnergyUsed.HasValue &&
                         session.Driver.BatteryCapacity.Value > 0)
                {
                    // Tính từ EnergyUsed và BatteryCapacity
                    var batteryCapacity = session.Driver.BatteryCapacity.Value;
                    var energyUsed = session.EnergyUsed.Value;
                    var socIncrease = (int)((energyUsed / batteryCapacity) * 100);
                    currentSOC = session.InitialSoc + socIncrease;
                    currentSOC = Math.Min(currentSOC, 100);
                }
                else
                {
                    // Nếu chưa có log và chưa có EnergyUsed, dùng InitialSoc
                    // Nhưng nếu InitialSOC đã >= target, không nên auto-stop ngay (cần thời gian để verify)
                    currentSOC = session.InitialSoc;
                }

                // Xác định target SOC
                // FinalSoc trong session có thể là:
                // 1. Target SOC từ reservation (được set khi start session từ reservation)
                // 2. null nếu là walk-in session (không có reservation)
                // Nếu FinalSoc = null, mặc định target = 100%
                int targetSOC = session.FinalSoc ?? 100;

                // ✅ Tránh auto-stop nếu session vừa mới start và SOC chưa thực sự tăng
                // Chỉ auto-stop nếu:
                // 1. Đã có log (chứng tỏ đã sạc được một lúc), HOẶC
                // 2. Đã có EnergyUsed > 0 (đã sạc được năng lượng), HOẶC  
                // 3. SOC đã tăng so với InitialSOC (chứng tỏ đã sạc được)
                bool hasActualChargingProgress = latestLog != null ||
                                                 (session.EnergyUsed.HasValue && session.EnergyUsed.Value > 0) ||
                                                 (currentSOC > session.InitialSoc);

                // Nếu chưa có progress thực sự và SOC vẫn bằng InitialSOC, không auto-stop
                // (tránh auto-stop ngay khi start nếu InitialSOC đã = target)
                if (!hasActualChargingProgress && currentSOC == session.InitialSoc && currentSOC >= targetSOC)
                {
                    _logger.LogDebug("Session {SessionId} just started with SOC={SOC}% (already at target), waiting for actual charging progress before auto-stop",
                        sessionId, currentSOC);
                    return false;
                }

                // Kiểm tra xem có đạt target chưa
                if (currentSOC >= targetSOC)
                {
                    _logger.LogInformation(
                        "Session {SessionId} reached target SOC: Current={CurrentSOC}%, Target={TargetSOC}%, Initial={InitialSOC}%. Auto-stopping...",
                        sessionId, currentSOC, targetSOC, session.InitialSoc);

                    // Tự động dừng session
                    var stopRequest = new ChargingSessionStopRequest
                    {
                        SessionId = sessionId,
                        FinalSOC = Math.Min(currentSOC, 100) // Đảm bảo không vượt quá 100%
                    };

                    var result = await chargingService.StopSessionAsync(stopRequest);

                    if (result != null)
                    {
                        _logger.LogInformation(
                            "Session {SessionId} auto-stopped successfully. FinalSOC={FinalSOC}%, FinalCost={FinalCost} VND, Duration={Duration} minutes",
                            sessionId, currentSOC, result.FinalCost, (int)sessionDuration.TotalMinutes);

                        // Dừng monitoring
                        await StopMonitoringAsync(sessionId);

                        // Gửi thông báo hoàn thành
                        await SendSessionCompleteNotificationAsync(sessionId);

                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to auto-stop session {SessionId}", sessionId);
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and auto-stopping session {SessionId}", sessionId);
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra và gửi thông báo khi gần sạc đầy (còn 10% so với targetSOC)
        /// </summary>
        private async Task CheckAndNotifyNearTargetSocAsync(int sessionId)
        {
            try
            {
                // Chỉ gửi thông báo một lần
                if (_nearTargetSocNotified.ContainsKey(sessionId))
                    return;

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .Include(s => s.SessionLogs)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Status != "in_progress" || session.Driver?.User == null)
                    return;

                // Xác định target SOC
                int targetSOC = session.FinalSoc ?? 100;

                // Lấy SOC hiện tại
                int currentSOC;
                var latestLog = session.SessionLogs?
                    .OrderByDescending(sl => sl.LogTime)
                    .FirstOrDefault();

                if (latestLog?.SocPercentage.HasValue == true)
                {
                    currentSOC = latestLog.SocPercentage.Value;
                }
                else if (session.Driver?.BatteryCapacity.HasValue == true &&
                         session.EnergyUsed.HasValue &&
                         session.Driver.BatteryCapacity.Value > 0)
                {
                    var batteryCapacity = session.Driver.BatteryCapacity.Value;
                    var energyUsed = session.EnergyUsed.Value;
                    var socIncrease = (int)((energyUsed / batteryCapacity) * 100);
                    currentSOC = session.InitialSoc + socIncrease;
                    currentSOC = Math.Min(currentSOC, 100);
                }
                else
                {
                    return; // Chưa có dữ liệu SOC
                }

                // Kiểm tra xem có gần target chưa (còn 10% so với targetSOC)
                int remainingToTarget = targetSOC - currentSOC;
                if (remainingToTarget <= 10 && remainingToTarget > 0)
                {
                    var userId = session.Driver.User.UserId;
                    var stationName = session.Point?.Station?.Name ?? "trạm sạc";
                    var estimatedMinutes = await EstimateRemainingTimeAsync(sessionId, targetSOC);

                    var title = "Sắp sạc đầy";
                    var message = $"Pin của bạn đang ở {currentSOC}% và sắp đạt mục tiêu {targetSOC}%.\n" +
                                 $"Còn khoảng {remainingToTarget}% nữa để hoàn tất.\n" +
                                 $"Thời gian ước tính: {estimatedMinutes.TotalMinutes:F0} phút.\n" +
                                 $"Trạm sạc: {stationName}";

                    await notificationService.SendNotificationAsync(
                        userId,
                        title,
                        message,
                        "charging_near_complete",
                        sessionId);

                    _nearTargetSocNotified[sessionId] = true;
                    _logger.LogInformation("Sent near target SOC notification for session {SessionId}: {CurrentSOC}% -> {TargetSOC}%",
                        sessionId, currentSOC, targetSOC);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and notifying near target SOC for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Kiểm tra và gửi thông báo gần đến giờ đặt chỗ
        /// </summary>
        private async Task CheckAndNotifyReservationReminderAsync(int sessionId)
        {
            try
            {
                // Chỉ gửi thông báo một lần
                if (_reservationReminderNotified.ContainsKey(sessionId))
                    return;

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.Reservation)
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Status != "in_progress" || session.Driver?.User == null)
                    return;

                // Chỉ gửi thông báo nếu session có reservation
                if (session.ReservationId == null || session.Reservation == null)
                    return;

                var reservation = session.Reservation;
                var now = DateTime.UtcNow;

                // Kiểm tra xem có reservation tiếp theo không (trong vòng 30 phút tới)
                // Lấy reservation tiếp theo của driver này (không phải reservation hiện tại)
                var upcomingReservation = await db.Reservations
                    .Include(r => r.Point)
                        .ThenInclude(p => p.Station)
                    .Where(r => r.DriverId == session.DriverId
                        && r.ReservationId != session.ReservationId
                        && r.Status == "booked"
                        && r.StartTime > now
                        && r.StartTime <= now.AddMinutes(30))
                    .OrderBy(r => r.StartTime)
                    .FirstOrDefaultAsync();

                if (upcomingReservation != null)
                {
                    var userId = session.Driver.User.UserId;
                    var timeUntilReservation = upcomingReservation.StartTime - now;
                    var stationName = upcomingReservation.Point?.Station?.Name ?? "trạm sạc";
                    var minutesUntil = (int)timeUntilReservation.TotalMinutes;

                    var title = "Nhắc nhở đặt chỗ sắp tới";
                    var message = $"Bạn có đặt chỗ sắp tới tại {stationName} trong {minutesUntil} phút nữa.\n" +
                                 $"Thời gian bắt đầu: {upcomingReservation.StartTime:HH:mm} ngày {upcomingReservation.StartTime:dd/MM/yyyy}.\n" +
                                 $"Vui lòng chuẩn bị để đến đúng giờ.";

                    await notificationService.SendNotificationAsync(
                        userId,
                        title,
                        message,
                        "reservation_reminder",
                        upcomingReservation.ReservationId);

                    _reservationReminderNotified[sessionId] = true;
                    _logger.LogInformation("Sent reservation reminder for session {SessionId}: upcoming reservation {ReservationId} in {Minutes} minutes",
                        sessionId, upcomingReservation.ReservationId, minutesUntil);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and notifying reservation reminder for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Lấy SOC hiện tại
        /// </summary>
        private async Task<int> GetCurrentSOCAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                    .Include(s => s.SessionLogs)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                    return 0;

                // Nếu có log, lấy từ log mới nhất
                var latestLog = session.SessionLogs?
                    .OrderByDescending(sl => sl.LogTime)
                    .FirstOrDefault();

                if (latestLog?.SocPercentage.HasValue == true)
                    return latestLog.SocPercentage.Value;

                // Nếu chưa có log, tính từ EnergyUsed và BatteryCapacity
                if (session.Driver?.BatteryCapacity.HasValue == true &&
                    session.EnergyUsed.HasValue &&
                    session.Driver.BatteryCapacity.Value > 0)
                {
                    var batteryCapacity = session.Driver.BatteryCapacity.Value;
                    var energyUsed = session.EnergyUsed.Value;
                    var socIncrease = (int)((energyUsed / batteryCapacity) * 100);
                    var currentSOC = session.InitialSoc + socIncrease;
                    return Math.Min(currentSOC, 100);
                }

                return session.InitialSoc;
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
            // Prevent overlapping executions
            if (!_monitoringInProgress.TryAdd(sessionId, true))
            {
                _logger.LogWarning("Monitoring for session {SessionId} is already in progress, skipping", sessionId);
                return;
            }

            try
            {
                if (!await IsSessionActiveAsync(sessionId))
                {
                    await StopMonitoringAsync(sessionId);
                    return;
                }

                _logger.LogDebug("🔍 [MonitorSession] Session {SessionId} - Starting monitoring cycle", sessionId);

                // Tự động tạo log mới và cập nhật SOC
                await AutoCreateSessionLogAsync(sessionId);

                // Tự động cập nhật EnergyUsed từ logs
                await UpdateEnergyUsedFromLogsAsync(sessionId);

                // Kiểm tra và gửi thông báo gần sạc đầy (còn 10% so với targetSOC)
                await CheckAndNotifyNearTargetSocAsync(sessionId);

                // Kiểm tra và gửi thông báo gần đến giờ đặt chỗ
                await CheckAndNotifyReservationReminderAsync(sessionId);

                // Kiểm tra và tự động dừng nếu đạt target SOC hoặc 100%
                var shouldAutoStop = await CheckAndAutoStopSessionAsync(sessionId);
                if (shouldAutoStop)
                {
                    _logger.LogInformation("Session {SessionId} reached target SOC, auto-stopping", sessionId);
                    return; // Session đã được dừng, không cần tiếp tục monitoring
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
                _logger.LogError(ex, "Error monitoring session {SessionId}", sessionId);
            }
            finally
            {
                _monitoringInProgress.TryRemove(sessionId, out _);
            }
        }

        /// <summary>
        /// Tự động tạo log cho session (mô phỏng nếu thiết bị không gửi)
        /// </summary>
        private async Task AutoCreateSessionLogAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

                var session = await db.ChargingSessions
                    .Include(s => s.Point)
                    .Include(s => s.Driver)
                    .Include(s => s.SessionLogs)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session?.Status != "in_progress" || session.Point == null)
                    return;

                var now = DateTime.UtcNow;

                // Kiểm tra log cuối cùng
                var lastLog = session.SessionLogs?
                    .OrderByDescending(l => l.LogTime)
                    .FirstOrDefault();

                // Nếu log cuối cùng quá cũ (> 30 giây) hoặc chưa có log, tạo log mới
                var shouldCreateLog = lastLog == null ||
                                      !lastLog.LogTime.HasValue ||
                                      (now - lastLog.LogTime!.Value).TotalSeconds > 30;

                if (!shouldCreateLog)
                {
                    _logger.LogDebug("⏭️ [AutoCreateSessionLog] Session {SessionId} - Skipping log creation (last log is {SecondsSinceLastLog:F0}s old, threshold: 30s)",
                        sessionId, lastLog != null && lastLog.LogTime.HasValue
                            ? (now - lastLog.LogTime.Value).TotalSeconds
                            : 0);
                    return;
                }

                _logger.LogDebug("📝 [AutoCreateSessionLog] Session {SessionId} - Creating new log (last log: {LastLogTime}, time since: {SecondsSinceLastLog:F0}s)",
                    sessionId,
                    lastLog?.LogTime?.ToString("HH:mm:ss") ?? "N/A",
                    lastLog != null && lastLog.LogTime.HasValue
                        ? (now - lastLog.LogTime.Value).TotalSeconds
                        : 0);

                // Tính toán SOC hiện tại
                var currentSOC = CalculateCurrentSOCFromLogs(session, lastLog);

                // Tính current power (dùng từ log cuối hoặc PowerOutput)
                var currentPower = lastLog?.CurrentPower ?? (decimal)(session.Point.PowerOutput ?? 50);

                // Tạo log mới
                var newLog = new EVCharging.BE.DAL.Entities.SessionLog
                {
                    SessionId = sessionId,
                    SocPercentage = currentSOC,
                    CurrentPower = currentPower,
                    Voltage = lastLog?.Voltage ?? 400, // Mặc định 400V
                    Temperature = lastLog?.Temperature ?? 25, // Mặc định 25°C
                    LogTime = now
                };

                db.SessionLogs.Add(newLog);

                // Cập nhật FinalSoc nếu SOC đã tăng
                if (currentSOC > session.InitialSoc)
                {
                    session.FinalSoc = currentSOC;
                }

                await db.SaveChangesAsync();

                // Log thông tin khi tạo log mới
                _logger.LogInformation(
                    "✅ [AutoCreateSessionLog] Session {SessionId} - Created new log: SOC={SOC}%, Power={Power}kW, Voltage={Voltage}V, Temp={Temp}°C, Time={LogTime}",
                    sessionId, currentSOC, currentPower, newLog.Voltage, newLog.Temperature, newLog.LogTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [AutoCreateSessionLog] Error auto-creating session log for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Tính SOC hiện tại dựa trên logs và EnergyUsed
        /// </summary>
        private int CalculateCurrentSOCFromLogs(EVCharging.BE.DAL.Entities.ChargingSession session, EVCharging.BE.DAL.Entities.SessionLog? lastLog)
        {
            // Nếu có log cuối, dùng SOC từ log đó (hoặc tính từ energy đã tăng)
            if (lastLog?.SocPercentage.HasValue == true)
            {
                // Nếu log có SOC, kiểm tra xem có cần cập nhật không dựa trên energy
                if (session.Driver?.BatteryCapacity.HasValue == true && session.EnergyUsed.HasValue)
                {
                    var batteryCapacity = session.Driver.BatteryCapacity.Value;
                    var energyUsed = session.EnergyUsed.Value;

                    // Tính SOC từ energy
                    var socFromEnergy = session.InitialSoc + (int)((energyUsed / batteryCapacity) * 100);
                    var socFromLog = lastLog.SocPercentage.Value;

                    // Dùng giá trị cao hơn (đảm bảo SOC không giảm)
                    return Math.Min(Math.Max(socFromLog, socFromEnergy), 100);
                }

                return lastLog.SocPercentage.Value;
            }

            // Nếu chưa có log, tính từ EnergyUsed và BatteryCapacity
            if (session.Driver?.BatteryCapacity.HasValue == true && session.EnergyUsed.HasValue)
            {
                var batteryCapacity = session.Driver.BatteryCapacity.Value;
                var energyUsed = session.EnergyUsed.Value;

                // Tính % SOC tăng thêm
                var socIncrease = (int)((energyUsed / batteryCapacity) * 100);
                var currentSOC = session.InitialSoc + socIncrease;

                return Math.Min(currentSOC, 100); // Không vượt quá 100%
            }

            // Fallback: dùng InitialSoc
            return session.InitialSoc;
        }

        /// <summary>
        /// Cập nhật SOC và EnergyUsed tự động
        /// </summary>
        private async Task UpdateEnergyUsedFromLogsAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

                var session = await db.ChargingSessions
                    .Include(s => s.SessionLogs)
                    .Include(s => s.Point)
                    .Include(s => s.Driver)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session?.Status != "in_progress" || session.SessionLogs == null)
                    return;

                // Tính energy từ logs (tích phân)
                var calculatedEnergy = CalculateEnergyUsedFromLogs(session);

                // Cập nhật EnergyUsed
                if (!session.EnergyUsed.HasValue ||
                    Math.Abs(session.EnergyUsed.Value - calculatedEnergy) > 0.01m)
                {
                    session.EnergyUsed = calculatedEnergy;
                    session.DurationMinutes = (int)(DateTime.UtcNow - session.StartTime).TotalMinutes;

                    // Cập nhật SOC dựa trên EnergyUsed
                    if (session.Driver?.BatteryCapacity.HasValue == true && session.Driver.BatteryCapacity.Value > 0)
                    {
                        var batteryCapacity = session.Driver.BatteryCapacity.Value;
                        var socIncrease = (int)((calculatedEnergy / batteryCapacity) * 100);
                        var newSOC = session.InitialSoc + socIncrease;

                        session.FinalSoc = Math.Min(newSOC, 100);
                    }

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating energy from logs for session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Tính energyUsed từ SessionLogs (tích phân power theo thời gian)
        /// </summary>
        private decimal CalculateEnergyUsedFromLogs(EVCharging.BE.DAL.Entities.ChargingSession session)
        {
            if (session.SessionLogs == null || !session.SessionLogs.Any())
                return 0;

            var logs = session.SessionLogs
                .Where(l => l.CurrentPower.HasValue && l.LogTime.HasValue)
                .OrderBy(l => l.LogTime)
                .ToList();

            if (logs.Count == 0)
                return 0;

            decimal totalEnergy = 0;
            var now = DateTime.UtcNow;

            // Nếu chỉ có 1 log
            if (logs.Count == 1)
            {
                var log = logs[0];
                var timeElapsed = (decimal)(now - log.LogTime!.Value).TotalHours;
                return log.CurrentPower!.Value * timeElapsed;
            }

            // Từ StartTime đến log đầu tiên
            var firstLog = logs[0];
            if (firstLog.LogTime.HasValue && firstLog.CurrentPower.HasValue)
            {
                var timeToFirst = (decimal)(firstLog.LogTime.Value - session.StartTime).TotalHours;
                if (timeToFirst > 0)
                    totalEnergy += firstLog.CurrentPower.Value * timeToFirst;
            }

            // Giữa các logs (tính trung bình power)
            for (int i = 1; i < logs.Count; i++)
            {
                var prevLog = logs[i - 1];
                var currentLog = logs[i];

                if (prevLog.LogTime.HasValue && currentLog.LogTime.HasValue &&
                    prevLog.CurrentPower.HasValue && currentLog.CurrentPower.HasValue)
                {
                    var timeDiff = (decimal)(currentLog.LogTime.Value - prevLog.LogTime.Value).TotalHours;
                    var avgPower = (prevLog.CurrentPower.Value + currentLog.CurrentPower.Value) / 2;
                    totalEnergy += avgPower * timeDiff;
                }
            }

            // Từ log cuối đến hiện tại
            var lastLog = logs.Last();
            if (lastLog.LogTime.HasValue && lastLog.CurrentPower.HasValue)
            {
                var timeFromLast = (decimal)(now - lastLog.LogTime.Value).TotalHours;
                if (timeFromLast > 0)
                    totalEnergy += lastLog.CurrentPower.Value * timeFromLast;
            }

            return totalEnergy;
        }

        /// <summary>
        /// Lấy trạng thái monitoring của session
        /// </summary>
        public async Task<Dictionary<string, object>> GetMonitoringStatusAsync(int sessionId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

                var session = await db.ChargingSessions
                    .Include(s => s.SessionLogs)
                    .Include(s => s.Point)
                    .Include(s => s.Driver)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                {
                    return new Dictionary<string, object>
                    {
                        ["sessionId"] = sessionId,
                        ["isMonitoring"] = false,
                        ["error"] = "Session not found"
                    };
                }

                var isMonitoring = _monitoringTimers.ContainsKey(sessionId);
                var isMonitoringInProgress = _monitoringInProgress.ContainsKey(sessionId);

                // Lấy log cuối cùng
                var lastLog = session.SessionLogs?
                    .OrderByDescending(l => l.LogTime)
                    .FirstOrDefault();

                // Đếm tổng số logs
                var totalLogs = session.SessionLogs?.Count ?? 0;

                // Tính thời gian từ log cuối cùng
                var timeSinceLastLog = lastLog?.LogTime.HasValue == true
                    ? (DateTime.UtcNow - lastLog.LogTime!.Value)
                    : (TimeSpan?)null;

                var status = new Dictionary<string, object>
                {
                    ["sessionId"] = sessionId,
                    ["sessionStatus"] = session.Status ?? "unknown",
                    ["isMonitoring"] = isMonitoring,
                    ["isMonitoringInProgress"] = isMonitoringInProgress,
                    ["totalLogs"] = totalLogs,
                    ["lastLogTime"] = lastLog?.LogTime ?? (DateTime?)null,
                    ["timeSinceLastLog"] = timeSinceLastLog.HasValue 
                        ? $"{timeSinceLastLog.Value.TotalSeconds:F0} seconds"
                        : "N/A",
                    ["lastLog"] = lastLog != null ? new Dictionary<string, object?>
                    {
                        ["logId"] = lastLog.LogId,
                        ["socPercentage"] = lastLog.SocPercentage,
                        ["currentPower"] = lastLog.CurrentPower,
                        ["voltage"] = lastLog.Voltage,
                        ["temperature"] = lastLog.Temperature,
                        ["logTime"] = lastLog.LogTime ?? (DateTime?)null
                    } : (Dictionary<string, object?>?)null,
                    ["sessionInfo"] = new Dictionary<string, object?>
                    {
                        ["startTime"] = session.StartTime,
                        ["initialSOC"] = session.InitialSoc,
                        ["finalSOC"] = session.FinalSoc,
                        ["energyUsed"] = session.EnergyUsed,
                        ["durationMinutes"] = session.DurationMinutes,
                        ["pointId"] = session.PointId,
                        ["driverId"] = session.DriverId
                    }
                };

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monitoring status for session {SessionId}", sessionId);
                return new Dictionary<string, object>
                {
                    ["sessionId"] = sessionId,
                    ["isMonitoring"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.LogInformation("Disposing SessionMonitorService...");

            // Dispose all timers
            foreach (var timer in _monitoringTimers.Values)
            {
                try
                {
                    timer?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing timer");
                }
            }

            _monitoringTimers.Clear();
            _activeSessions.Clear();
            _monitoringInProgress.Clear();
            _nearTargetSocNotified.Clear();
            _reservationReminderNotified.Clear();

            _disposed = true;
            _logger.LogInformation("SessionMonitorService disposed");
        }
    }
