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

                    // ✅ ĐÃ BỎ: Auto-cancel NO-SHOW - Vì đã có cọc tiền, nếu không tới sạc thì mất cọc, không cần tự động hủy
                    // Logic cũ: Tự động hủy reservation nếu quá StartTime + 15 phút mà chưa check-in
                    // Lý do bỏ: Đã có cọc tiền, nếu không tới sạc thì mất cọc, không cần tự động hủy reservation
                    // Reservation sẽ giữ nguyên status "booked" cho đến khi:
                    // - User tự hủy
                    // - User check-in (chuyển sang "checked_in")
                    // - Hết thời gian slot (có thể tự động chuyển sang "completed" hoặc "no_show" nếu cần)
                    
                    // Code cũ đã được comment:
                    // const int NO_SHOW_GRACE_MINUTES = 15;
                    // var noShowBorder = now.AddMinutes(-NO_SHOW_GRACE_MINUTES);
                    // var toCancel = await db.Reservations...
                    // ... (đã bỏ logic tự động hủy)

                    // 1) Auto-cancel reservation đã hết thời gian slot mà chưa check-in
                    // Nếu reservation có status = "booked" (chưa check-in) và EndTime <= now → chuyển thành "cancelled"
                    var expiredReservations = await db.Reservations
                        .Include(r => r.Driver)
                            .ThenInclude(d => d.User)
                        .Include(r => r.Point)
                            .ThenInclude(p => p.Station)
                        .Where(r => r.Status == "booked" && r.EndTime <= now)
                        .ToListAsync(stoppingToken);

                    if (expiredReservations.Count > 0)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                        foreach (var r in expiredReservations)
                        {
                            r.Status = "cancelled";
                            r.UpdatedAt = now;

                            // Gửi thông báo cho user
                            if (r.Driver?.User != null)
                            {
                                var userId = r.Driver.User.UserId;
                                var stationName = r.Point?.Station?.Name ?? "trạm sạc";
                                var endTime = r.EndTime;

                                var title = "Đặt chỗ đã hết hạn";
                                var message = $"Đặt chỗ của bạn tại {stationName} đã hết hạn do không check-in trong thời gian slot.\n" +
                                             $"Thời gian kết thúc: {endTime:HH:mm} ngày {endTime:dd/MM/yyyy}\n" +
                                             $"Mã đặt chỗ: {r.ReservationCode}\n" +
                                             $"Lưu ý: Cọc đã đặt sẽ không được hoàn lại.";

                                try
                                {
                                    await notificationService.SendNotificationAsync(
                                        userId,
                                        title,
                                        message,
                                        "reservation_expired",
                                        r.ReservationId);
                                }
                                catch (Exception notifEx)
                                {
                                    _logger.LogError(notifEx, "Error sending notification for expired reservation {ReservationId}", r.ReservationId);
                                }
                            }
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Auto-cancelled {Count} expired reservations (no check-in before end time).", expiredReservations.Count);
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
                            // ✅ Set DepositPaymentStatus = "failed" khi auto-cancel do chưa thanh toán cọc
                            // Nếu đang "pending" hoặc null → set thành "failed" để đánh dấu thanh toán thất bại (timeout)
                            if (r.DepositPaymentStatus != "success")
                            {
                                r.DepositPaymentStatus = "failed";
                            }
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
