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
                    .Include(r => r.Point)
                        .ThenInclude(p => p.Station)
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
                            var stationName = reservation.Point?.Station?.Name ?? "trạm sạc";
                            var stationAddress = reservation.Point?.Station?.Address ?? "";
                            var pointId = reservation.PointId;
                            var connectorType = reservation.Point?.ConnectorType ?? "N/A";
                            var powerOutput = reservation.Point?.PowerOutput ?? 0;
                            
                            // Tạo message chi tiết với thông tin điểm sạc
                            var message = $"Bạn có đặt chỗ sắp tới tại {stationName}";
                            if (!string.IsNullOrEmpty(stationAddress))
                            {
                                message += $" ({stationAddress})";
                            }
                            message += $" - Điểm sạc #{pointId}";
                            if (!string.IsNullOrEmpty(connectorType) && connectorType != "N/A")
                            {
                                message += $", loại {connectorType}";
                            }
                            if (powerOutput > 0)
                            {
                                message += $", công suất {powerOutput}kW";
                            }
                            message += $" vào lúc {reservation.StartTime:HH:mm dd/MM/yyyy}. Vui lòng đến đúng giờ.";
                            
                            db.Notifications.Add(new EVCharging.BE.DAL.Entities.Notification
                            {
                                UserId = userId,
                                Title = "Nhắc nhở đặt chỗ sắp tới",
                                Message = message,
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
