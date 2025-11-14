using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EVCharging.BE.DAL;
using EVCharging.BE.Services.Services.Notification;
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
        private readonly IServiceProvider _serviceProvider;

        public ReservationExpiryWorker(
            ILogger<ReservationExpiryWorker> logger,
            IDbContextFactory<EvchargingManagementContext> dbFactory,
            IOptions<ReservationBackgroundOptions> opt,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _dbFactory = dbFactory;
            _opt = opt.Value;
            _serviceProvider = serviceProvider;
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

                    // 1) Auto-cancel NO-SHOW: quá StartTime + 15 phút mà chưa check-in (theo yêu cầu)
                    const int NO_SHOW_GRACE_MINUTES = 15; // 15 phút theo yêu cầu
                    var noShowBorder = now.AddMinutes(-NO_SHOW_GRACE_MINUTES);
                    var toCancel = await db.Reservations
                        .Include(r => r.Driver)
                            .ThenInclude(d => d.User)
                        .Include(r => r.Point)
                            .ThenInclude(p => p.Station)
                        .Where(r => r.Status == "booked" && r.StartTime < noShowBorder)
                        .ToListAsync(stoppingToken);

                    if (toCancel.Count > 0)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                        foreach (var r in toCancel)
                        {
                            // Tùy rule của bạn: "cancelled" hoặc "no_show"
                            r.Status = "cancelled";
                            r.UpdatedAt = now;

                            // Gửi thông báo cho user
                            if (r.Driver?.User != null)
                            {
                                var userId = r.Driver.User.UserId;
                                var stationName = r.Point?.Station?.Name ?? "trạm sạc";
                                var startTime = r.StartTime;

                                var title = "Đặt chỗ đã bị hủy tự động";
                                var message = $"Đặt chỗ của bạn tại {stationName} đã bị hủy tự động do không check-in sau 15 phút từ thời gian bắt đầu.\n" +
                                             $"Thời gian đặt chỗ: {startTime:HH:mm} ngày {startTime:dd/MM/yyyy}\n" +
                                             $"Mã đặt chỗ: {r.ReservationCode}\n" +
                                             $"Lưu ý: Cọc đã đặt sẽ không được hoàn lại.";

                                try
                                {
                                    await notificationService.SendNotificationAsync(
                                        userId,
                                        title,
                                        message,
                                        "reservation_auto_cancelled",
                                        r.ReservationId);
                                }
                                catch (Exception notifEx)
                                {
                                    _logger.LogError(notifEx, "Error sending notification for auto-cancelled reservation {ReservationId}", r.ReservationId);
                                }
                            }
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Auto-cancelled {Count} expired reservations (no check-in after 15 minutes).", toCancel.Count);
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
                                                       p.PaymentType == "deposit"))
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
