namespace EVCharging.BE.Services.Services
{
    /// <summary>
    /// Interface cho service gửi email
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Gửi email đặt lại mật khẩu
        /// </summary>
        /// <param name="toEmail">Email người nhận</param>
        /// <param name="userName">Tên người dùng</param>
        /// <param name="resetToken">Token đặt lại mật khẩu</param>
        /// <param name="resetUrl">URL đặt lại mật khẩu</param>
        /// <returns>Task</returns>
        Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken, string resetUrl);

        /// <summary>
        /// Gửi email thông báo đặt lại mật khẩu thành công
        /// </summary>
        /// <param name="toEmail">Email người nhận</param>
        /// <param name="userName">Tên người dùng</param>
        /// <returns>Task</returns>
        Task SendPasswordResetSuccessEmailAsync(string toEmail, string userName);

        /// <summary>
        /// Gửi email thông báo chung
        /// </summary>
        /// <param name="toEmail">Email người nhận</param>
        /// <param name="subject">Tiêu đề email</param>
        /// <param name="body">Nội dung email</param>
        /// <param name="isHtml">Có phải HTML không</param>
        /// <returns>Task</returns>
        Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
    }
}
