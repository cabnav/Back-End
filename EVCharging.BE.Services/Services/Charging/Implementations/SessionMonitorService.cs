using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
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
                }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

                _monitoringTimers[sessionId] = timer;
                _logger.LogInformation("Started monitoring session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting monitoring for session {SessionId}", sessionId);
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

                _logger.LogInformation("Session {SessionId} completed: {Message}", sessionId, message);
                
                // TODO: Send notification to user
                // await _notificationService.SendAsync(session.Driver.UserId, "Charging Complete", message);
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

                var session = await db.ChargingSessions
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                    return;

                _logger.LogWarning("Session {SessionId} error: {ErrorMessage}", sessionId, errorMessage);
                
                // TODO: Send error notification to user and admin
                // await _notificationService.SendAsync(session.Driver.UserId, "Charging Alert", errorMessage);
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

                // Lấy SOC hiện tại
                var currentSOC = await GetCurrentSOCAsync(sessionId);

                // Xác định target SOC
                // Nếu có FinalSoc được set (từ reservation hoặc user), dùng FinalSoc
                // Nếu không, mặc định là 100%
                int targetSOC = session.FinalSoc.HasValue ? session.FinalSoc.Value : 100;

                // Kiểm tra xem có đạt target chưa
                if (currentSOC >= targetSOC)
                {
                    _logger.LogInformation(
                        "Session {SessionId} reached target SOC: Current={CurrentSOC}%, Target={TargetSOC}%. Auto-stopping...",
                        sessionId, currentSOC, targetSOC);

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
                            "Session {SessionId} auto-stopped successfully. FinalSOC={FinalSOC}%, FinalCost={FinalCost} VND",
                            sessionId, currentSOC, result.FinalCost);
                        
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

                // Tự động tạo log mới và cập nhật SOC
                await AutoCreateSessionLogAsync(sessionId);

                // Tự động cập nhật EnergyUsed từ logs
                await UpdateEnergyUsedFromLogsAsync(sessionId);

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
                    return;

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-creating session log for session {SessionId}", sessionId);
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

            _disposed = true;
            _logger.LogInformation("SessionMonitorService disposed");
        }
    }
}
