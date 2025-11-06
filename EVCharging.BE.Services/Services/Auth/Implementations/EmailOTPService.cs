using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Auth;
using EVCharging.BE.Services.Services.Notification;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Auth.Implementations
{
    public class EmailOTPService : IEmailOTPService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IEmailService _emailService;
        private readonly Random _random = new();

        public EmailOTPService(EvchargingManagementContext db, IEmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        public async Task<bool> SendOTPAsync(string email, string purpose = "registration")
        {
            try
            {
                Console.WriteLine($"[OTP] Starting SendOTP for email: {email}");
                
                // Normalize email (trim and lowercase)
                var normalizedEmail = email.Trim().ToLowerInvariant();
                
                // Check if email already registered (case-insensitive comparison)
                // Load all emails and compare normalized versions to handle case/whitespace differences
                var existingEmails = await _db.Users.Select(u => u.Email).ToListAsync();
                var emailExists = existingEmails.Any(e => e.Trim().ToLowerInvariant() == normalizedEmail);
                
                if (emailExists)
                {
                    var foundEmail = existingEmails.First(e => e.Trim().ToLowerInvariant() == normalizedEmail);
                    Console.WriteLine($"[OTP] Email already registered: {email} (found in DB: {foundEmail})");
                    return false; // Email already registered
                }

                Console.WriteLine($"[OTP] Email not registered, generating OTP...");

                // Generate 6-digit OTP
                var otpCode = _random.Next(100000, 999999).ToString();

                // Set expiration to 30 minutes from now
                var expiresAt = DateTime.UtcNow.AddMinutes(30);

                // Invalidate all existing OTPs for this email (case-insensitive)
                var existingOtps = await _db.EmailOTPs.ToListAsync();
                var otpsToInvalidate = existingOtps
                    .Where(o => o.Email.Trim().ToLowerInvariant() == normalizedEmail 
                        && !o.IsUsed 
                        && o.ExpiresAt > DateTime.UtcNow)
                    .ToList();

                if (otpsToInvalidate.Any())
                {
                    Console.WriteLine($"[OTP] Invalidating {otpsToInvalidate.Count} old OTPs");
                    foreach (var existingOtp in otpsToInvalidate)
                    {
                        existingOtp.IsUsed = true;
                    }
                    await _db.SaveChangesAsync();
                }

                // Create new OTP (store normalized email)
                var emailOtp = new EmailOTP
                {
                    Email = normalizedEmail,
                    OtpCode = otpCode,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IsUsed = false,
                    Purpose = purpose
                };

                Console.WriteLine($"[OTP] Created OTP: {otpCode} for {normalizedEmail} (original: {email})");
                _db.EmailOTPs.Add(emailOtp);
                await _db.SaveChangesAsync();
                Console.WriteLine($"[OTP] OTP saved to database successfully");

                // Send email (use original email for display)
                var subject = purpose == "registration" 
                    ? "Mã xác nhận đăng ký - EV Charging System"
                    : "Mã xác nhận - EV Charging System";

                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h2 style='color: #2c3e50; text-align: center;'>Mã xác nhận email</h2>
                            
                            <p>Xin chào,</p>
                            
                            <p>Chúng tôi nhận được yêu cầu xác nhận email <strong>{email}</strong>.</p>
                            
                            <div style='background-color: #f8f9fa; border: 2px solid #3498db; border-radius: 10px; padding: 20px; text-align: center; margin: 30px 0;'>
                                <p style='font-size: 14px; color: #666; margin: 0 0 10px 0;'>Mã xác nhận của bạn:</p>
                                <h1 style='color: #3498db; font-size: 36px; letter-spacing: 8px; margin: 0; font-family: monospace;'>{otpCode}</h1>
                            </div>
                            
                            <p><strong>Lưu ý:</strong></p>
                            <ul>
                                <li>Mã này sẽ hết hạn sau <strong>30 phút</strong></li>
                                <li>Vui lòng không chia sẻ mã này với bất kỳ ai</li>
                                <li>Nếu bạn không yêu cầu mã này, vui lòng bỏ qua email này</li>
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

                Console.WriteLine($"[OTP] Sending email to {email}...");
                await _emailService.SendEmailAsync(email, subject, body, true);
                Console.WriteLine($"[OTP] Email sent successfully!");

                return true;
            }
            catch (Exception ex)
            {
                // Log exception for debugging
                Console.WriteLine($"[OTP ERROR] Error in SendOTPAsync: {ex.Message}");
                Console.WriteLine($"[OTP ERROR] Inner Exception: {ex.InnerException?.Message}");
                Console.WriteLine($"[OTP ERROR] StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> VerifyOTPAsync(string email, string otpCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
                {
                    Console.WriteLine($"[VerifyOTP] Invalid input - email or OTP code is empty");
                    return false;
                }

                // Normalize email for comparison
                var normalizedEmail = email.Trim().ToLowerInvariant();
                Console.WriteLine($"[VerifyOTP] Verifying OTP for email: '{normalizedEmail}' (original: '{email}')");

                // Find valid OTP (case-insensitive email comparison)
                var allOtps = await _db.EmailOTPs
                    .Where(o => o.OtpCode == otpCode 
                        && !o.IsUsed 
                        && o.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                var otp = allOtps
                    .Where(o => o.Email.Trim().ToLowerInvariant() == normalizedEmail)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();

                if (otp == null)
                {
                    Console.WriteLine($"[VerifyOTP] OTP not found or invalid for email: '{normalizedEmail}'");
                    return false;
                }

                Console.WriteLine($"[VerifyOTP] OTP found and valid for email: '{normalizedEmail}' (stored as: '{otp.Email}')");

                // Mark OTP as used
                otp.IsUsed = true;
                await _db.SaveChangesAsync();
                Console.WriteLine($"[VerifyOTP] OTP marked as used successfully");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VerifyOTP ERROR] {ex.Message}");
                Console.WriteLine($"[VerifyOTP ERROR] StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> HasValidOTPAsync(string email)
        {
            try
            {
                // Normalize email for comparison
                var normalizedEmail = email.Trim().ToLowerInvariant();
                
                var allOtps = await _db.EmailOTPs
                    .Where(o => !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                var hasValidOTP = allOtps
                    .Any(o => o.Email.Trim().ToLowerInvariant() == normalizedEmail);

                return hasValidOTP;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<int> DeleteExpiredOTPsAsync()
        {
            try
            {
                var expiredOtps = await _db.EmailOTPs
                    .Where(o => o.ExpiresAt < DateTime.UtcNow || o.IsUsed)
                    .ToListAsync();

                if (expiredOtps.Any())
                {
                    _db.EmailOTPs.RemoveRange(expiredOtps);
                    await _db.SaveChangesAsync();
                }

                return expiredOtps.Count;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}

