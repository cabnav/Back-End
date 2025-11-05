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

                    // 1) Tìm các session "in_progress" có reservation liên quan đã quá EndTime
                    var sessionsWithReservation = await db.ChargingSessions
                        .Include(s => s.Reservation)
                        .Where(s => 
                            s.Status == "in_progress" && 
                            !s.EndTime.HasValue &&
                            s.ReservationId.HasValue &&
                            s.Reservation != null &&
                            s.Reservation.EndTime < now &&
                            (s.Reservation.Status == "checked_in" || s.Reservation.Status == "in_progress"))
                        .ToListAsync(stoppingToken);

                    // 2) ✅ Tìm các walk-in session (không có ReservationId) có maxEndTime trong Notes đã đến
                    var walkInSessionsToStop = await db.ChargingSessions
                        .Where(s => 
                            s.Status == "in_progress" && 
                            !s.EndTime.HasValue &&
                            !s.ReservationId.HasValue &&
                            !string.IsNullOrEmpty(s.Notes))
                        .ToListAsync(stoppingToken);
                    
                    _logger.LogInformation($"SessionAutoStopWorker: Found {walkInSessionsToStop.Count} walk-in sessions to check for maxEndTime");

                    var walkInSessionsWithMaxEndTime = new List<DAL.Entities.ChargingSession>();
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
                                        walkInSessionsWithMaxEndTime.Add(session);
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

                    // Combine cả hai loại sessions
                    var sessionsToStop = new List<DAL.Entities.ChargingSession>();
                    sessionsToStop.AddRange(sessionsWithReservation);
                    sessionsToStop.AddRange(walkInSessionsWithMaxEndTime);

                    if (sessionsToStop.Count > 0)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var chargingService = scope.ServiceProvider.GetRequiredService<EVCharging.BE.Services.Services.Charging.IChargingService>();

                        foreach (var session in sessionsToStop)
                        {
                            try
                            {
                                // Auto-stop session
                                var stopRequest = new EVCharging.BE.Common.DTOs.Charging.ChargingSessionStopRequest
                                {
                                    SessionId = session.SessionId,
                                    FinalSOC = session.FinalSoc ?? session.InitialSoc // Use current SOC or initial if not available
                                };

                                var result = await chargingService.StopSessionAsync(stopRequest);
                                
                                if (result != null)
                                {
                                    _logger.LogInformation($"✅ Auto-stopped walk-in session {session.SessionId} (maxEndTime reached). ChargingPoint {session.PointId} is now available for next reservation.");
                                }
                                else
                                {
                                    _logger.LogWarning($"⚠️ Failed to auto-stop session {session.SessionId}.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error auto-stopping session {session.SessionId}.");
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

