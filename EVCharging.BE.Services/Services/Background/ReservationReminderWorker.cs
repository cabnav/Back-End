using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EVCharging.BE.Services.Services.Background;

public class ReservationReminderWorker : BackgroundService
{
    private readonly ILogger<ReservationReminderWorker> _logger;
    private readonly IDbContextFactory<EvchargingManagementContext> _dbFactory;
    private readonly ReservationBackgroundOptions _opt;

    public ReservationReminderWorker(
        ILogger<ReservationReminderWorker> logger,
        IDbContextFactory<EvchargingManagementContext> dbFactory,
        IOptions<ReservationBackgroundOptions> opt)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReservationReminderWorker started. Interval={Sec}s", _opt.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);

                var now = DateTime.UtcNow;
                var reminderTime = now.AddMinutes(_opt.ReminderMinutes);

                var upcomingReservations = await db.Reservations
                    .Where(r => r.Status == "booked" && r.StartTime <= reminderTime && r.StartTime > now)
                    .OrderBy(r => r.StartTime)
                    .ToListAsync(stoppingToken);

                if (upcomingReservations.Count > 0)
                {
                    foreach (var reservation in upcomingReservations)
                    {
                        var userId = await db.DriverProfiles
                            .Where(d => d.DriverId == reservation.DriverId)
                            .Select(d => d.UserId)
                            .FirstOrDefaultAsync(stoppingToken);

                        if (userId > 0)
                        {
                            db.Notifications.Add(new EVCharging.BE.DAL.Entities.Notification
                            {
                                UserId = userId,
                                Title = "Nhắc nhở đặt chỗ sắp tới",
                                Message = $"Bạn có đặt chỗ sắp tới tại điểm sạc {reservation.PointId} vào lúc {reservation.StartTime:HH:mm dd/MM/yyyy}.",
                                Type = "reservation_reminder",
                                IsRead = false,
                                CreatedAt = now
                            });
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("ReservationReminderWorker: sent {Count} reminders.", upcomingReservations.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReservationReminderWorker");
            }

            await Task.Delay(TimeSpan.FromSeconds(_opt.CheckIntervalSeconds), stoppingToken);
        }
    }
}
