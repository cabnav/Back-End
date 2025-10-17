using EVCharging.BE.Services.Services;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace EVCharging.BE.Services.Services.Implementations
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
        private readonly string _baseUrl;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["Email:SmtpUsername"] ?? "";
            _smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
            _fromEmail = _configuration["Email:FromEmail"] ?? "";
            _fromName = _configuration["Email:FromName"] ?? "EV Charging System";
            _baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
        }

        /// <summary>
        /// Gửi email đặt lại mật khẩu
        /// </summary>
        public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken, string resetUrl)
        {
            var subject = "Đặt lại mật khẩu - EV Charging System";
            var resetLink = $"{_baseUrl}/reset-password?token={resetToken}";
            
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50; text-align: center;'>Đặt lại mật khẩu</h2>
                        
                        <p>Xin chào <strong>{userName}</strong>,</p>
                        
                        <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn trong hệ thống EV Charging.</p>
                        
                        <p>Để đặt lại mật khẩu, vui lòng nhấp vào liên kết bên dưới:</p>
                        
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{resetLink}' 
                               style='background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Đặt lại mật khẩu
                            </a>
                        </div>
                        
                        <p><strong>Lưu ý:</strong></p>
                        <ul>
                            <li>Liên kết này sẽ hết hạn sau 1 giờ</li>
                            <li>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này</li>
                            <li>Để bảo mật, không chia sẻ liên kết này với bất kỳ ai</li>
                        </ul>
                        
                        <p>Nếu bạn gặp khó khăn, vui lòng liên hệ với chúng tôi.</p>
                        
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
