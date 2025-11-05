using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EVCharging.BE.Services.Services.Background
{
    /// <summary>
    /// Tự động chuyển reservation status từ "checked_in" sang "in_progress" khi StartTime đến
    /// </summary>
    public class ReservationStatusUpdateWorker : BackgroundService
    {
        private readonly ILogger<ReservationStatusUpdateWorker> _logger;
        private readonly IDbContextFactory<EvchargingManagementContext> _dbFactory;
        private readonly int _checkIntervalSeconds = 60; // Check every minute

        public ReservationStatusUpdateWorker(
            ILogger<ReservationStatusUpdateWorker> logger,
            IDbContextFactory<EvchargingManagementContext> dbFactory)
        {
            _logger = logger;
            _dbFactory = dbFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReservationStatusUpdateWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);

                    var now = DateTime.UtcNow;

                    // Tìm các reservation có status = "checked_in" và StartTime <= now (đã đến thời gian bắt đầu)
                    var reservationsToUpdate = await db.Reservations
                        .Where(r => 
                            r.Status == "checked_in" && 
                            r.StartTime <= now &&
                            db.ChargingSessions.Any(s => s.ReservationId == r.ReservationId && s.Status == "in_progress"))
                        .ToListAsync(stoppingToken);

                    if (reservationsToUpdate.Count > 0)
                    {
                        foreach (var reservation in reservationsToUpdate)
                        {
                            reservation.Status = "in_progress";
                            reservation.UpdatedAt = now;
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"Updated {reservationsToUpdate.Count} reservation(s) from 'checked_in' to 'in_progress'.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ReservationStatusUpdateWorker.");
                }

                await Task.Delay(TimeSpan.FromSeconds(_checkIntervalSeconds), stoppingToken);
            }
        }
    }
}

