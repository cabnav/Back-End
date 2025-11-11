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
    }
}

