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
        private readonly IDbContextFactory<EvchargingManagementContext> _dbFactory;
        private readonly ReservationBackgroundOptions _opt;

        public ReservationExpiryWorker(
            ILogger<ReservationExpiryWorker> logger,
            IDbContextFactory<EvchargingManagementContext> dbFactory,
            IOptions<ReservationBackgroundOptions> opt)
        {
            _logger = logger;
            _dbFactory = dbFactory;
            _opt = opt.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReservationExpiryWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);

                    var now = DateTime.UtcNow;

                    // 1) Auto-cancel NO-SHOW: quá StartTime + NoShowGraceMinutes mà chưa check-in
                    var noShowBorder = now.AddMinutes(-_opt.NoShowGraceMinutes);
                    var toCancel = await db.Reservations
                        .Where(r => r.Status == "booked" && r.StartTime < noShowBorder)
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

                    // 2) Auto-cancel reservation chưa thanh toán cọc sau 5 phút
                    // Nếu reservation được tạo hơn 5 phút nhưng chưa có deposit payment thành công → hủy
                    var depositTimeoutBorder = now.AddMinutes(-5); // 5 phút trước
                    
                    // Tối ưu: Sử dụng LEFT JOIN để lấy reservations không có deposit payment thành công
                    var toCancelNoDeposit = await db.Reservations
                        .Where(r => r.Status == "booked" && 
                                   r.CreatedAt.HasValue &&
                                   r.CreatedAt.Value < depositTimeoutBorder &&
                                   !db.Payments.Any(p => p.ReservationId == r.ReservationId &&
                                                       p.PaymentStatus == "success" &&
                                                       p.PaymentType == "deposit" &&
                                                       p.Amount == 20000m))
                        .ToListAsync(stoppingToken);

                    if (toCancelNoDeposit.Count > 0)
                    {
                        foreach (var r in toCancelNoDeposit)
                        {
                            r.Status = "cancelled";
                            r.UpdatedAt = now;
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Auto-cancelled {Count} reservations without deposit payment after 5 minutes.", toCancelNoDeposit.Count);
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
