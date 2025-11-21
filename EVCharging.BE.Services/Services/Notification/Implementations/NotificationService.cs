using System.Linq;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationEntity = EVCharging.BE.DAL.Entities.Notification;

namespace EVCharging.BE.Services.Services.Notification.Implementations
{
    /// <summary>
    /// Service implementation để gửi và lưu thông báo vào database
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly EvchargingManagementContext _db;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(EvchargingManagementContext db, ILogger<NotificationService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gửi thông báo cho người dùng
        /// </summary>
        public async Task SendNotificationAsync(int userId, string title, string message, string type, int? relatedId = null)
        {
            try
            {
                // Kiểm tra user có tồn tại không
                var userExists = await _db.Users.AnyAsync(u => u.UserId == userId);
                if (!userExists)
                {
                    _logger.LogWarning("User {UserId} not found, cannot send notification", userId);
                    return;
                }

                var notification = new NotificationEntity
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Type = type,
                    RelatedId = relatedId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Notifications.Add(notification);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Notification sent to user {UserId}: {Title}", userId, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}: {Message}", userId, ex.Message);
                // Không throw exception để không làm gián đoạn flow chính
            }
        }

        /// <summary>
        /// Gửi thông báo cho tất cả admin khi có incident report mới
        /// </summary>
        public async Task NotifyAdminIncidentAsync(int reportId, string title, string reporterName, string reporterRole)
        {
            try
            {
                Console.WriteLine($"[NOTIFICATION SERVICE] Starting to notify admins about incident report {reportId}");
                _logger.LogInformation("Starting to notify admins about incident report {ReportId}", reportId);

                // Lấy tất cả admin users - case-insensitive comparison
                var adminUsers = await _db.Users
                    .Where(u => u.Role != null && u.Role.ToLower() == "admin")
                    .Select(u => new { u.UserId, u.Name, u.Role })
                    .ToListAsync();

                Console.WriteLine($"[NOTIFICATION SERVICE] Found {adminUsers.Count} admin user(s) in database");
                _logger.LogInformation("Found {Count} admin user(s) in database", adminUsers.Count);

                if (!adminUsers.Any())
                {
                    Console.WriteLine($"[NOTIFICATION SERVICE WARNING] No admin users found! Checking all users...");
                    _logger.LogWarning("No admin users found to notify about incident report {ReportId}. Checking all users...", reportId);
                    
                    // Debug: Log tất cả users để xem role của họ
                    var allUsers = await _db.Users
                        .Select(u => new { u.UserId, u.Name, u.Role })
                        .ToListAsync();
                    
                    var usersInfo = string.Join(", ", allUsers.Select(u => $"UserId={u.UserId}, Name={u.Name}, Role={u.Role ?? "NULL"}"));
                    Console.WriteLine($"[NOTIFICATION SERVICE] All users in database: {usersInfo}");
                    _logger.LogWarning("All users in database: {Users}", usersInfo);
                    
                    return;
                }

                // Tạo thông báo cho từng admin
                var message = $"Báo cáo sự cố mới từ {reporterRole} {reporterName}: {title}";
                var notificationTitle = $"Báo cáo sự cố mới - #{reportId}";

                var adminIds = string.Join(", ", adminUsers.Select(a => a.UserId));
                Console.WriteLine($"[NOTIFICATION SERVICE] Sending notifications to {adminUsers.Count} admin(s): {adminIds}");
                _logger.LogInformation("Sending notifications to {Count} admin(s): {AdminIds}", 
                    adminUsers.Count, adminIds);

                foreach (var admin in adminUsers)
                {
                    Console.WriteLine($"[NOTIFICATION SERVICE] Sending notification to admin {admin.UserId} ({admin.Name})");
                    _logger.LogInformation("Sending notification to admin {AdminId} ({AdminName})", admin.UserId, admin.Name);
                    
                    await SendNotificationAsync(
                        userId: admin.UserId,
                        title: notificationTitle,
                        message: message,
                        type: "incident_report",
                        relatedId: reportId
                    );
                    
                    Console.WriteLine($"[NOTIFICATION SERVICE] Notification sent successfully to admin {admin.UserId}");
                    _logger.LogInformation("Notification sent successfully to admin {AdminId}", admin.UserId);
                }

                Console.WriteLine($"[NOTIFICATION SERVICE] Successfully sent notifications to {adminUsers.Count} admin(s) about incident report {reportId}");
                _logger.LogInformation("Successfully sent notifications to {Count} admin(s) about incident report {ReportId}", 
                    adminUsers.Count, reportId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NOTIFICATION SERVICE ERROR] Error notifying admins about incident report {reportId}: {ex.Message}");
                Console.WriteLine($"[NOTIFICATION SERVICE ERROR] StackTrace: {ex.StackTrace}");
                _logger.LogError(ex, "Error notifying admins about incident report {ReportId}: {Message}. StackTrace: {StackTrace}", 
                    reportId, ex.Message, ex.StackTrace);
                // Không throw exception để không làm gián đoạn flow chính
            }
        }
    }
}

