namespace EVCharging.BE.Services.Services.Notification
{
    /// <summary>
    /// Service để gửi và lưu thông báo vào database
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Gửi thông báo cho người dùng
        /// </summary>
        /// <param name="userId">ID người dùng</param>
        /// <param name="title">Tiêu đề thông báo</param>
        /// <param name="message">Nội dung thông báo</param>
        /// <param name="type">Loại thông báo</param>
        /// <param name="relatedId">ID liên quan (sessionId, reservationId, v.v.)</param>
        /// <returns>Task</returns>
        Task SendNotificationAsync(int userId, string title, string message, string type, int? relatedId = null);

        /// <summary>
        /// Gửi thông báo cho tất cả admin khi có incident report mới
        /// </summary>
        /// <param name="reportId">ID của incident report</param>
        /// <param name="title">Tiêu đề incident report</param>
        /// <param name="reporterName">Tên người báo cáo</param>
        /// <param name="reporterRole">Vai trò người báo cáo (Driver/Staff)</param>
        /// <returns>Task</returns>
        Task NotifyAdminIncidentAsync(int reportId, string title, string reporterName, string reporterRole);
    }
}
