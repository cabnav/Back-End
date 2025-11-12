using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace EVCharging.BE.Services.Services.Notification.Implementations
{
    /// <summary>
    /// Service gửi email sử dụng MailKit
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["Email:SmtpUsername"] ?? "";
            _smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
            _fromEmail = _configuration["Email:FromEmail"] ?? "";
            _fromName = _configuration["Email:FromName"] ?? "EV Charging System";
        }

        /// <summary>
        /// Gửi email thông báo đặt lại mật khẩu thành công
        /// </summary>
        public async Task SendPasswordResetSuccessEmailAsync(string toEmail, string userName)
        {
            var subject = "Mật khẩu đã được đặt lại thành công - EV Charging System";
            
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #27ae60; text-align: center;'>Mật khẩu đã được đặt lại thành công</h2>
                        
                        <p>Xin chào <strong>{userName}</strong>,</p>
                        
                        <p>Mật khẩu của bạn đã được đặt lại thành công vào lúc {DateTime.Now:dd/MM/yyyy HH:mm}.</p>
                        
                        <p>Nếu bạn không thực hiện thao tác này, vui lòng:</p>
                        <ul>
                            <li>Liên hệ với chúng tôi ngay lập tức</li>
                            <li>Kiểm tra tài khoản của bạn</li>
                            <li>Thay đổi mật khẩu nếu cần thiết</li>
                        </ul>
                        
                        <p>Cảm ơn bạn đã sử dụng dịch vụ của chúng tôi!</p>
                        
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
                        
                        <p style='font-size: 12px; color: #666; text-align: center;'>
                            Email này được gửi tự động từ hệ thống EV Charging.<br>
                            Vui lòng không trả lời email này.
                        </p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(toEmail, subject, body, true);
        }

        /// <summary>
        /// Gửi email thông báo chung
        /// </summary>
        public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_fromName, _fromEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                if (isHtml)
                {
                    message.Body = new TextPart("html")
                    {
                        Text = body
                    };
                }
                else
                {
                    message.Body = new TextPart("plain")
                    {
                        Text = body
                    };
                }

                using var client = new SmtpClient();
                await client.ConnectAsync(_smtpHost, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                Console.WriteLine($"Email sent successfully to {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email to {toEmail}: {ex.Message}");
                throw;
            }
        }
    }
}
