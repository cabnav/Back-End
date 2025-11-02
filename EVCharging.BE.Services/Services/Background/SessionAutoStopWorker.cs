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

                    // Tìm các session "in_progress" có reservation liên quan đã quá EndTime
                    // Join session với reservation qua PointId, sau đó filter:
                    // - Session StartTime nằm trong khoảng reservation (StartTime <= session.StartTime <= EndTime)
                    // - Reservation EndTime đã qua (< now)
                    // - Reservation status là checked_in hoặc in_progress
                    var sessionsToStop = await db.ChargingSessions
                        .Where(s => s.Status == "in_progress" && !s.EndTime.HasValue)
                        .Join(
                            db.Reservations
                                .Where(r => r.EndTime < now && (r.Status == "checked_in" || r.Status == "in_progress")),
                            session => session.PointId,
                            reservation => reservation.PointId,
                            (session, reservation) => new { Session = session, Reservation = reservation }
                        )
                        .Where(x => 
                            x.Reservation.StartTime <= x.Session.StartTime &&
                            x.Session.StartTime <= x.Reservation.EndTime &&
                            x.Reservation.EndTime < now)
                        .Select(x => x.Session)
                        .Distinct()
                        .ToListAsync(stoppingToken);

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
                                    _logger.LogInformation($"Auto-stopped session {session.SessionId} due to reservation end time.");
                                }
                                else
                                {
                                    _logger.LogWarning($"Failed to auto-stop session {session.SessionId}.");
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

