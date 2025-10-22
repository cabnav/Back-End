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

namespace EVCharging.BE.Services.Services.Background
{
    /// <summary>
    /// Tự động hủy các reservation đã hết hạn (status = booked, EndTime + grace < now).
    /// </summary>
    public class ReservationExpiryWorker : BackgroundService
    {
        private readonly ILogger<ReservationExpiryWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ReservationBackgroundOptions _opt;

        public ReservationExpiryWorker(
            ILogger<ReservationExpiryWorker> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<ReservationBackgroundOptions> opt)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _opt = opt.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReservationExpiryWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();

                    var now = DateTime.UtcNow;
                    var graceBorder = now.AddMinutes(-_opt.ExpireGraceMinutes);

                    var toCancel = await db.Reservations
                        .Where(r => r.Status == "booked" && r.EndTime < graceBorder)
                        .ToListAsync(stoppingToken);

                    if (toCancel.Count > 0)
                    {
                        foreach (var r in toCancel)
                        {
                            // Tùy rule của bạn: "cancelled" hoặc "no_show"
                            r.Status = "cancelled";
                            r.UpdatedAt = now;
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Auto-cancelled {Count} expired reservations.", toCancel.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ReservationExpiryWorker.");
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _opt.CheckIntervalSeconds)), stoppingToken);
            }
        }
    }
}
