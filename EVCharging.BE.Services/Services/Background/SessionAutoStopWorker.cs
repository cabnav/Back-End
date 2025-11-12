using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EVCharging.BE.Services.Services.Background
{
    /// <summary>
    /// Tự động dừng các session đã quá end time của reservation
    /// </summary>
    public class SessionAutoStopWorker : BackgroundService
    {
        private readonly ILogger<SessionAutoStopWorker> _logger;
        private readonly IDbContextFactory<EvchargingManagementContext> _dbFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _checkIntervalSeconds = 60; // Check every minute

        public SessionAutoStopWorker(
            ILogger<SessionAutoStopWorker> logger,
            IDbContextFactory<EvchargingManagementContext> dbFactory,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _dbFactory = dbFactory;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionAutoStopWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);

                    var now = DateTime.UtcNow;

                    // 1) Tìm các session "in_progress" có reservation liên quan đã đến hoặc quá EndTime
                    // ✅ Sử dụng AsNoTracking() để tránh tracking issues và chỉ lấy SessionId, FinalSoc, InitialSoc
                    var sessionsWithReservation = await db.ChargingSessions
                        .AsNoTracking()
                        .Include(s => s.Reservation)
                        .Where(s => 
                            s.Status == "in_progress" && 
                            !s.EndTime.HasValue &&
                            s.ReservationId.HasValue &&
                            s.Reservation != null &&
                            s.Reservation.EndTime <= now && // ✅ Đổi từ < thành <= để dừng ngay khi đến EndTime
                            (s.Reservation.Status == "checked_in" || s.Reservation.Status == "in_progress"))
                        .Select(s => new { s.SessionId, s.FinalSoc, s.InitialSoc, s.ReservationId, ReservationEndTime = s.Reservation.EndTime })
                        .ToListAsync(stoppingToken);

                    if (sessionsWithReservation.Count > 0)
                    {
                        _logger.LogInformation($"SessionAutoStopWorker: Found {sessionsWithReservation.Count} reservation session(s) to stop (EndTime <= now).");
                    }

                    // 2) ✅ Tìm các walk-in session (không có ReservationId) có maxEndTime trong Notes đã đến
                    // ✅ Sử dụng AsNoTracking() để tránh tracking issues
                    var walkInSessionsToStop = await db.ChargingSessions
                        .AsNoTracking()
                        .Where(s => 
                            s.Status == "in_progress" && 
                            !s.EndTime.HasValue &&
                            !s.ReservationId.HasValue &&
                            !string.IsNullOrEmpty(s.Notes))
                        .Select(s => new { s.SessionId, s.Notes, s.FinalSoc, s.InitialSoc })
                        .ToListAsync(stoppingToken);
                    
                    _logger.LogInformation($"SessionAutoStopWorker: Found {walkInSessionsToStop.Count} walk-in sessions to check for maxEndTime");

                    var walkInSessionsWithMaxEndTime = new List<(int SessionId, int? FinalSoc, int InitialSoc)>();
                    foreach (var session in walkInSessionsToStop)
                    {
                        try
                        {
                            // Parse Notes để lấy maxEndTime
                            if (System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(session.Notes) is { } notesJson
                                && notesJson.TryGetProperty("maxEndTime", out var maxEndTimeElement))
                            {
                                DateTime? maxEndTime = null;
                                
                                // Try parse as DateTime (if serialized as DateTime)
                                if (maxEndTimeElement.TryGetDateTime(out var parsedDateTime))
                                {
                                    maxEndTime = parsedDateTime;
                                }
                                // Try parse as string (if serialized as string)
                                else if (maxEndTimeElement.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var dateTimeString = maxEndTimeElement.GetString();
                                    if (!string.IsNullOrEmpty(dateTimeString) && DateTime.TryParse(dateTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedFromString))
                                    {
                                        maxEndTime = parsedFromString;
                                    }
                                }
                                
                                if (maxEndTime.HasValue)
                                {
                                    if (maxEndTime.Value <= now)
                                    {
                                        walkInSessionsWithMaxEndTime.Add((session.SessionId, session.FinalSoc, session.InitialSoc));
                                        _logger.LogInformation($"✅ Found walk-in session {session.SessionId} with maxEndTime {maxEndTime.Value:O} <= now {now:O} - WILL STOP");
                                    }
                                    else
                                    {
                                        _logger.LogDebug($"Walk-in session {session.SessionId} maxEndTime {maxEndTime.Value:O} > now {now:O} - NOT YET");
                                    }
                                }
                                else
                                {
                                    _logger.LogDebug($"Walk-in session {session.SessionId} has Notes but could not parse maxEndTime");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Error parsing Notes for session {session.SessionId}: {ex.Message}");
                        }
                    }

                    // ✅ Combine cả hai loại sessions - chỉ lưu SessionId và FinalSoc/InitialSoc
                    var sessionsToStop = new List<(int SessionId, int? FinalSoc, int InitialSoc)>();
                    foreach (var s in sessionsWithReservation)
                    {
                        sessionsToStop.Add((s.SessionId, s.FinalSoc, s.InitialSoc));
                    }
                    sessionsToStop.AddRange(walkInSessionsWithMaxEndTime);

                    if (sessionsToStop.Count > 0)
                    {
                        // ✅ Tạo scope cho mỗi batch để tránh DbContext conflicts
                        using var scope = _serviceProvider.CreateScope();
                        var chargingService = scope.ServiceProvider.GetRequiredService<EVCharging.BE.Services.Services.Charging.IChargingService>();

                        foreach (var (sessionId, finalSoc, initialSoc) in sessionsToStop)
                        {
                            try
                            {
                                // Auto-stop session
                                // ✅ StopSessionAsync sẽ tự query lại từ DB và kiểm tra status, nên không cần re-check ở đây
                                var stopRequest = new EVCharging.BE.Common.DTOs.Charging.ChargingSessionStopRequest
                                {
                                    SessionId = sessionId,
                                    FinalSOC = finalSoc ?? initialSoc // Use current SOC or initial if not available
                                };

                                var result = await chargingService.StopSessionAsync(stopRequest);
                                
                                if (result != null)
                                {
                                    _logger.LogInformation($"✅ Auto-stopped session {sessionId}. ChargingPoint {result.ChargingPointId} is now available for next reservation.");
                                }
                                else
                                {
                                    // ✅ Session có thể đã bị stop bởi user hoặc không còn active - không phải lỗi nghiêm trọng
                                    _logger.LogDebug($"Session {sessionId} could not be auto-stopped (may have been stopped by user or is no longer active).");
                                }
                            }
                            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
                            {
                                // ✅ Xử lý concurrency exception - session có thể đã bị thay đổi bởi user
                                _logger.LogWarning(ex, $"Concurrency conflict when auto-stopping session {sessionId}. Session may have been stopped by user.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error auto-stopping session {sessionId}.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SessionAutoStopWorker.");
                }

                await Task.Delay(TimeSpan.FromSeconds(_checkIntervalSeconds), stoppingToken);
            }
        }
    }
}