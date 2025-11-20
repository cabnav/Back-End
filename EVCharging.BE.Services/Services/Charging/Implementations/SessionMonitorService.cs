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

                // Create timer to check session every 30 seconds
                // ✅ Delay first check by 1 minute to avoid checking immediately after session start
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
                }, null, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30)); // ✅ First check after 1 minute, then every 30 seconds

                _monitoringTimers[sessionId] = timer;
                _logger.LogInformation("✅ [StartMonitoring] Started monitoring session {SessionId} - First check in 1 minute, then every 30 seconds", sessionId);
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
                    .Include(s => s.Reservation) // ✅ Load reservation để lấy TargetSoc mới nhất
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Status != "in_progress")
                    return false;

                // ✅ Tránh auto-stop ngay khi session vừa start (< 1 phút)
                // Session cần thời gian để sạc và tạo logs
                var sessionDuration = DateTime.UtcNow - session.StartTime;
                if (sessionDuration.TotalMinutes < 1)
                {
                    _logger.LogDebug("Session {SessionId} is too new ({Duration:F1} minutes), skipping auto-stop check",
                        sessionId, sessionDuration.TotalMinutes);
                    return false;
                }

                // ✅ Tính SOC hiện tại hoàn toàn dựa trên năng lượng đã sạc
                var currentSOC = CalculateCurrentSOCFromEnergy(session);
                if (session.CurrentSoc != currentSOC)
                {
                    session.CurrentSoc = currentSOC;
                    await db.SaveChangesAsync();
                }

                // Lấy log mới nhất (chỉ để kiểm tra tiến trình, không dùng cho SOC)
                var latestLog = session.SessionLogs?
                    .Where(sl => sl.LogTime.HasValue && sl.LogTime.Value >= session.StartTime)
                    .OrderByDescending(sl => sl.LogTime)
                    .FirstOrDefault();

                // ✅ Xác định target SOC từ reservation (lấy mới nhất từ database)
                // Logic:
                // - Mặc định targetSOC = 100% (nếu không có reservation - walk-in)
                // - Nếu có reservation, lấy TargetSoc từ reservation (có thể thay đổi theo thời gian)
                // - Nếu reservation.TargetSoc = null, mặc định = 100%
                // ✅ QUAN TRỌNG: KHÔNG dùng session.FinalSoc làm fallback vì có thể là giá trị cũ không chính xác
                int targetSOC = 100; // Mặc định 100%
                string targetSOCSource = "mặc định (100%)"; // Track nguồn targetSOC
                
                if (session.ReservationId.HasValue && session.Reservation != null)
                {
                    // ✅ CÓ reservation: Lấy TargetSoc từ reservation
                    if (session.Reservation.TargetSoc.HasValue)
                    {
                        targetSOC = session.Reservation.TargetSoc.Value;
                        targetSOCSource = $"reservation.TargetSoc ({targetSOC}%)";
                    }
                    else
                    {
                        // reservation.TargetSoc = null, giữ nguyên 100% mặc định
                        targetSOCSource = "reservation không có TargetSoc, mặc định 100%";
                    }
                }
                else
                {
                    // ✅ KHÔNG có reservation (walk-in): Luôn dùng 100% mặc định
                    // KHÔNG dùng session.FinalSoc vì có thể là giá trị cũ/sai
                    targetSOCSource = "walk-in, mặc định 100%";
                }

                // ✅ QUAN TRỌNG: Validation targetSOC
                // Đảm bảo targetSOC >= InitialSOC, > 0, và >= 50% (giá trị hợp lý tối thiểu)
                const int MIN_VALID_TARGET_SOC = 50; // Giá trị targetSOC tối thiểu hợp lý (50%)
                var originalTargetSOC = targetSOC;
                
                if (targetSOC <= 0 || targetSOC < session.InitialSoc || targetSOC < MIN_VALID_TARGET_SOC)
                {
                    _logger.LogWarning(
                        "Session {SessionId} - TargetSOC không hợp lệ: TargetSOC={TargetSOC}%, InitialSOC={InitialSOC}%, MinValid={MinValid}%. Nguồn: {Source}. Sử dụng targetSOC={FallbackTarget}% mặc định.",
                        sessionId, targetSOC, session.InitialSoc, MIN_VALID_TARGET_SOC, targetSOCSource, 100);
                    
                    // ✅ Fallback: Nếu targetSOC không hợp lệ, dùng 100% mặc định
                    // Đảm bảo targetSOC >= InitialSOC và >= MIN_VALID_TARGET_SOC
                    if (session.InitialSoc >= 100)
                    {
                        // Nếu InitialSOC >= 100, dùng InitialSOC (SOC đã đầy)
                        targetSOC = session.InitialSoc;
                    }
                    else if (session.InitialSoc >= MIN_VALID_TARGET_SOC)
                    {
                        // Nếu InitialSOC >= 50% nhưng < 100%, dùng 100% (mặc định)
                        targetSOC = 100;
                    }
                    else
                    {
                        // Nếu InitialSOC < 50%, dùng 50% (tối thiểu hợp lý)
                        targetSOC = MIN_VALID_TARGET_SOC;
                    }
                    
                    targetSOCSource = $"fallback sau validation (original: {originalTargetSOC}%)";
                }

                // Đảm bảo targetSOC không vượt quá 100%
                targetSOC = Math.Min(targetSOC, 100);
                
                // Đảm bảo targetSOC >= InitialSOC (double-check sau khi validation)
                if (targetSOC < session.InitialSoc)
                {
                    _logger.LogWarning(
                        "Session {SessionId} - TargetSOC ({TargetSOC}%) < InitialSOC ({InitialSOC}%). Điều chỉnh thành InitialSOC.",
                        sessionId, targetSOC, session.InitialSoc);
                    targetSOC = session.InitialSoc;
                    targetSOCSource = $"điều chỉnh từ InitialSOC";
                }
                
                // ✅ Đảm bảo targetSOC >= MIN_VALID_TARGET_SOC (final check)
                if (targetSOC < MIN_VALID_TARGET_SOC)
                {
                    _logger.LogWarning(
                        "Session {SessionId} - TargetSOC sau validation ({TargetSOC}%) vẫn < MIN_VALID ({MinValid}%). Điều chỉnh thành {MinValid}%.",
                        sessionId, targetSOC, MIN_VALID_TARGET_SOC, MIN_VALID_TARGET_SOC);
                    targetSOC = MIN_VALID_TARGET_SOC;
                    targetSOCSource = $"điều chỉnh thành MIN_VALID ({MIN_VALID_TARGET_SOC}%)";
                }

                _logger.LogDebug(
                    "Session {SessionId} - InitialSOC: {InitialSOC}%, CurrentSOC: {CurrentSOC}%, TargetSOC: {TargetSOC}% (nguồn: {Source})",
                    sessionId, session.InitialSoc, currentSOC, targetSOC, targetSOCSource);

                // ✅ Tránh auto-stop nếu session vừa mới start và SOC chưa thực sự tăng
                // Chỉ auto-stop nếu:
                // 1. Đã có log SAU initial log (chứng tỏ đã sạc được một lúc), HOẶC
                // 2. Đã có EnergyUsed > 0 (đã sạc được năng lượng), HOẶC  
                // 3. SOC đã tăng so với InitialSOC (chứng tỏ đã sạc được)
                // LƯU Ý: latestLog đã được filter chỉ lấy log sau StartTime, nên không cần kiểm tra isInitialLog nữa
                bool hasActualChargingProgress = false;
                
                // ✅ Đếm số log hợp lệ (sau StartTime)
                var validLogs = session.SessionLogs?
                    .Where(sl => sl.LogTime.HasValue && sl.LogTime.Value >= session.StartTime)
                    .ToList() ?? new List<DAL.Entities.SessionLog>();
                var totalValidLogs = validLogs.Count;
                
                if (latestLog != null && totalValidLogs > 0)
                {
                    // ✅ Kiểm tra xem log này có phải là initial log không
                    // Initial log thường có LogTime gần StartTime (trong vòng 1 phút)
                    var isInitialLog = totalValidLogs == 1 || 
                                       (latestLog.LogTime.HasValue && latestLog.LogTime.Value <= session.StartTime.AddMinutes(1));
                    
                    // Chỉ coi là có progress nếu có nhiều hơn 1 log hợp lệ HOẶC log đó không phải là initial log
                    if (totalValidLogs > 1 || !isInitialLog)
                    {
                        hasActualChargingProgress = true;
                    }
                }
                
                // Hoặc có EnergyUsed > 0 (đã sạc được năng lượng)
                if (!hasActualChargingProgress && session.EnergyUsed.HasValue && session.EnergyUsed.Value > 0)
                {
                    hasActualChargingProgress = true;
                }
                
                // Hoặc SOC đã tăng so với InitialSOC (chứng tỏ đã sạc được)
                // ✅ QUAN TRỌNG: Chỉ coi là progress nếu currentSOC > InitialSOC (không chỉ >=)
                if (!hasActualChargingProgress && currentSOC > session.InitialSoc)
                {
                    hasActualChargingProgress = true;
                }

                // ✅ QUAN TRỌNG: Nếu chưa có progress thực sự, KHÔNG auto-stop
                // Tránh auto-stop ngay khi start nếu InitialSOC đã >= target (chưa sạc được gì)
                if (!hasActualChargingProgress)
                {
                    _logger.LogDebug("Session {SessionId} - InitialSOC: {InitialSOC}%, CurrentSOC: {CurrentSOC}%, TargetSOC: {TargetSOC}%. Chưa có tiến trình sạc thực sự, không auto-stop.",
                        sessionId, session.InitialSoc, currentSOC, targetSOC);
                    return false;
                }

                // ✅ Kiểm tra xem có đạt target chưa
                // ✅ QUAN TRỌNG: Auto-stop khi:
                // 1. currentSOC >= targetSOC (đã đạt hoặc vượt target từ reservation)
                // 2. currentSOC > InitialSOC (đã có tiến trình sạc thực sự - SOC đã tăng)
                // 3. targetSOC >= InitialSOC (đã được validate ở trên)
                // 4. Có tiến trình sạc thực sự (đã có log hoặc EnergyUsed > 0 hoặc SOC đã tăng)
                // Logic: Khi InitialSOC sạc tăng lên = FinalSOC (targetSOC), tự động dừng
                // ✅ QUAN TRỌNG: Thêm điều kiện currentSOC > InitialSOC để đảm bảo đã có tiến trình sạc
                if (targetSOC >= session.InitialSoc && 
                    currentSOC >= targetSOC && 
                    currentSOC > session.InitialSoc && // ✅ Đảm bảo SOC đã tăng so với InitialSOC
                    hasActualChargingProgress)
                {
                    _logger.LogInformation(
                        "Session {SessionId} đã đạt target SOC: Current={CurrentSOC}%, Target={TargetSOC}% (từ reservation mới nhất), Initial={InitialSOC}%. SOC đã tăng từ {InitialSOC}% lên {CurrentSOC}%. Tự động dừng...",
                        sessionId, currentSOC, targetSOC, session.InitialSoc, session.InitialSoc, currentSOC);

                    // ✅ QUAN TRỌNG: FinalSOC = targetSOC (từ reservation mới nhất), không phải currentSOC
                    // Điều này đảm bảo:
                    // - Nếu reservation.TargetSoc = 100, FinalSOC = 100
                    // - Nếu reservation.TargetSoc = 80, FinalSOC = 80 (ngay cả khi currentSOC có thể cao hơn một chút)
                    // - Nếu reservation.TargetSoc thay đổi trong quá trình sạc, FinalSOC sẽ cập nhật theo
                    // - Đảm bảo FinalSOC phản ánh đúng mục tiêu từ reservation mới nhất
                    int finalSOCToUse = targetSOC; // Dùng targetSOC từ reservation mới nhất
                    
                    // Đảm bảo FinalSOC không vượt quá 100%
                    finalSOCToUse = Math.Min(finalSOCToUse, 100);
                    
                    // Đảm bảo FinalSOC >= InitialSOC (SOC không thể giảm)
                    finalSOCToUse = Math.Max(finalSOCToUse, session.InitialSoc);

                    // Nếu log cuối cùng chưa phản ánh SOC đạt target, thêm log cuối ở mức target/final
                    if (latestLog?.SocPercentage.GetValueOrDefault() < finalSOCToUse)
                    {
                        var finalLog = new EVCharging.BE.DAL.Entities.SessionLog
                        {
                            SessionId = sessionId,
                            SocPercentage = finalSOCToUse,
                            CurrentPower = 0,
                            Voltage = latestLog?.Voltage ?? 400,
                            Temperature = latestLog?.Temperature ?? 25,
                            LogTime = DateTime.UtcNow
                        };

                        db.SessionLogs.Add(finalLog);
                        await db.SaveChangesAsync();

                        _logger.LogInformation(
                            "Session {SessionId} - Added final SOC log at {FinalSOC}% before auto-stop to reflect completion.",
                            sessionId, finalSOCToUse);
                    }

                    // Tự động dừng session
                    var stopRequest = new ChargingSessionStopRequest
                    {
                        SessionId = sessionId,
                        FinalSOC = finalSOCToUse
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
                    .Include(s => s.Reservation) // ✅ Load reservation để lấy TargetSoc mới nhất
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Status != "in_progress" || session.Driver?.User == null)
                    return;

                // ✅ Xác định target SOC từ reservation (lấy mới nhất từ database)
                // Logic giống CheckAndAutoStopSessionAsync:
                // - Mặc định targetSOC = 100% (nếu không có reservation - walk-in)
                // - Nếu có reservation, lấy TargetSoc từ reservation
                // - Nếu reservation.TargetSoc = null, mặc định = 100%
                // ✅ QUAN TRỌNG: KHÔNG dùng session.FinalSoc làm fallback
                int targetSOC = 100; // Mặc định 100%
                
                if (session.ReservationId.HasValue && session.Reservation != null)
                {
                    // ✅ CÓ reservation: Lấy TargetSoc từ reservation
                    if (session.Reservation.TargetSoc.HasValue)
                    {
                        targetSOC = session.Reservation.TargetSoc.Value;
                    }
                    // Nếu reservation.TargetSoc = null, giữ nguyên 100% mặc định
                }
                else
                {
                    // ✅ KHÔNG có reservation (walk-in): Luôn dùng 100% mặc định
                    // KHÔNG dùng session.FinalSoc
                }

                // ✅ QUAN TRỌNG: Validation targetSOC (giống logic trong CheckAndAutoStopSessionAsync)
                // Đảm bảo targetSOC >= InitialSOC, > 0, và >= 50%
                const int MIN_VALID_TARGET_SOC = 50;
                
                if (targetSOC <= 0 || targetSOC < session.InitialSoc || targetSOC < MIN_VALID_TARGET_SOC)
                {
                    _logger.LogWarning(
                        "Session {SessionId} - TargetSOC không hợp lệ trong CheckAndNotifyNearTargetSocAsync: TargetSOC={TargetSOC}%, InitialSOC={InitialSOC}%. Sử dụng targetSOC mặc định.",
                        sessionId, targetSOC, session.InitialSoc);
                    
                    // Fallback: Đảm bảo targetSOC >= InitialSOC và >= MIN_VALID_TARGET_SOC
                    if (session.InitialSoc >= 100)
                    {
                        targetSOC = session.InitialSoc;
                    }
                    else if (session.InitialSoc >= MIN_VALID_TARGET_SOC)
                    {
                        targetSOC = 100;
                    }
                    else
                    {
                        targetSOC = MIN_VALID_TARGET_SOC;
                    }
                }

                // Đảm bảo targetSOC không vượt quá 100%
                targetSOC = Math.Min(targetSOC, 100);
                
                // Đảm bảo targetSOC >= InitialSOC (double-check sau khi validation)
                targetSOC = Math.Max(targetSOC, session.InitialSoc);
                
                // Đảm bảo targetSOC >= MIN_VALID_TARGET_SOC (final check)
                if (targetSOC < MIN_VALID_TARGET_SOC)
                {
                    targetSOC = MIN_VALID_TARGET_SOC;
                }

                var driver = session.Driver;
                var batteryCapacity = driver?.BatteryCapacity;
                if (batteryCapacity.HasValue != true ||
                    batteryCapacity.Value <= 0 ||
                    !session.EnergyUsed.HasValue)
                {
                    return; // Chưa có dữ liệu năng lượng để tính SOC
                }

                var currentSOC = CalculateCurrentSOCFromEnergy(session);
                if (session.CurrentSoc != currentSOC)
                {
                    session.CurrentSoc = currentSOC;
                    await db.SaveChangesAsync();
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

                if (session.Driver?.BatteryCapacity.HasValue == true &&
                    session.Driver.BatteryCapacity.Value > 0 &&
                    session.EnergyUsed.HasValue)
                {
                    return CalculateCurrentSOCFromEnergy(session);
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

                _logger.LogInformation("🔍 [MonitorSession] Session {SessionId} - Starting monitoring cycle at {Time}", 
                    sessionId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

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

                _logger.LogInformation("✅ [MonitorSession] Session {SessionId} - Completed monitoring cycle at {Time}", 
                    sessionId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [MonitorSession] Error monitoring session {SessionId} at {Time}", 
                    sessionId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
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

                // Tính toán SOC hiện tại dựa trên năng lượng đã sạc
                var currentSOC = CalculateCurrentSOCFromEnergy(session);

                // Tính current power (dùng từ log cuối hoặc PowerOutput)
                var currentPower = lastLog?.CurrentPower ?? (decimal)(session.Point.PowerOutput ?? 50);

                // Tạo log mới
                session.CurrentSoc = currentSOC;

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

                // ✅ KHÔNG cập nhật FinalSoc ở đây
                // FinalSoc đã được set từ reservation.TargetSoc khi start session
                // FinalSoc sẽ chỉ được cập nhật khi stop session (auto-stop hoặc thủ công)
                // Giữ nguyên targetSOC từ reservation để đảm bảo logic auto-stop đúng

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
        /// Tính SOC hiện tại dựa trên năng lượng đã sạc
        /// </summary>
        private int CalculateCurrentSOCFromEnergy(EVCharging.BE.DAL.Entities.ChargingSession session)
        {
            if (session.Driver?.BatteryCapacity.HasValue == true &&
                session.Driver.BatteryCapacity.Value > 0 &&
                session.EnergyUsed.HasValue)
            {
                var batteryCapacity = session.Driver.BatteryCapacity.Value;
                var energyUsed = Math.Max(session.EnergyUsed.Value, 0m);

                var socIncrease = (int)((energyUsed / batteryCapacity) * 100);
                var currentSOC = session.InitialSoc + socIncrease;
                currentSOC = Math.Min(currentSOC, 100);

                return Math.Max(currentSOC, session.InitialSoc);
            }

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

                    if (session.Driver?.BatteryCapacity.HasValue == true && session.Driver.BatteryCapacity.Value > 0)
                    {
                        var currentSOC = CalculateCurrentSOCFromEnergy(session);
                        if (session.CurrentSoc != currentSOC)
                        {
                            session.CurrentSoc = currentSOC;
                        }
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
        public async Task<Dictionary<string, object?>> GetMonitoringStatusAsync(int sessionId)
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
                    return new Dictionary<string, object?>
                    {
                        ["sessionId"] = sessionId,
                        ["isMonitoring"] = false,
                        ["error"] = "Không tìm thấy phiên sạc"
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
                TimeSpan? timeSinceLastLog = null;
                if (lastLog != null && lastLog.LogTime.HasValue)
                {
                    timeSinceLastLog = DateTime.UtcNow - lastLog.LogTime.Value;
                }

                Dictionary<string, object?>? lastLogDict = null;
                if (lastLog != null)
                {
                    lastLogDict = new Dictionary<string, object?>
                    {
                        ["logId"] = lastLog.LogId,
                        ["socPercentage"] = lastLog.SocPercentage,
                        ["currentPower"] = lastLog.CurrentPower,
                        ["voltage"] = lastLog.Voltage,
                        ["temperature"] = lastLog.Temperature,
                        ["logTime"] = lastLog.LogTime
                    };
                }

                var status = new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId,
                    ["sessionStatus"] = session.Status ?? "unknown",
                    ["isMonitoring"] = isMonitoring,
                    ["isMonitoringInProgress"] = isMonitoringInProgress,
                    ["totalLogs"] = totalLogs,
                    ["lastLogTime"] = lastLog?.LogTime,
                    ["timeSinceLastLog"] = timeSinceLastLog.HasValue
                        ? $"{timeSinceLastLog.Value.TotalSeconds:F0} seconds"
                        : "N/A",
                    ["lastLog"] = lastLogDict,
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
                return new Dictionary<string, object?>
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
}