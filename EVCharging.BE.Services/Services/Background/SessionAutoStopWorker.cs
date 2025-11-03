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
                    // Sử dụng ReservationId trực tiếp thay vì join qua PointId (chính xác và hiệu quả hơn)
                    var sessionsToStop = await db.ChargingSessions
                        .Include(s => s.Reservation)
                        .Where(s => 
                            s.Status == "in_progress" && 
                            !s.EndTime.HasValue &&
                            s.ReservationId.HasValue &&
                            s.Reservation != null &&
                            s.Reservation.EndTime < now &&
                            (s.Reservation.Status == "checked_in" || s.Reservation.Status == "in_progress"))
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

