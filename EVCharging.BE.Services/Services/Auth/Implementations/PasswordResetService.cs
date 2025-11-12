using EVCharging.BE.Common.DTOs.Auth;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace EVCharging.BE.Services.Services.Auth.Implementations
{
    /// <summary>
    /// Service quản lý đặt lại mật khẩu
    /// </summary>
    public class PasswordResetService : IPasswordResetService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IConfiguration _configuration;
        private readonly IEmailOTPService _emailOTPService;
        private readonly IEmailService _emailService;

        public PasswordResetService(EvchargingManagementContext db, IConfiguration configuration, IEmailOTPService emailOTPService, IEmailService emailService)
        {
            _db = db;
            _configuration = configuration;
            _emailOTPService = emailOTPService;
            _emailService = emailService;
        }

        /// <summary>
        /// Gửi OTP để đặt lại mật khẩu
        /// </summary>
        public async Task<CreatePasswordResetTokenResponse> CreatePasswordResetTokenAsync(CreatePasswordResetTokenRequest request)
        {
            try
            {
                // Tìm user theo email
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (user == null)
                {
                    return new CreatePasswordResetTokenResponse
                    {
                        Success = false,
                        Message = "Email không tồn tại trong hệ thống"
                    };
                }

                // Gửi OTP qua email
                var otpSent = await _emailOTPService.SendOTPAsync(request.Email, "password-reset");
                
                if (!otpSent)
                {
                    return new CreatePasswordResetTokenResponse
                    {
                        Success = false,
                        Message = "Không thể gửi mã OTP. Vui lòng thử lại sau."
                    };
                }

                // OTP hết hạn sau 30 phút (theo EmailOTPService)
                var expiresAt = DateTime.UtcNow.AddMinutes(30);

                return new CreatePasswordResetTokenResponse
                {
                    Success = true,
                    Message = "Mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra email và nhập mã OTP để tiếp tục.",
                    Token = null, // Không trả về token ở bước này
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending password reset OTP: {ex.Message}");
                return new CreatePasswordResetTokenResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi gửi mã OTP đặt lại mật khẩu"
                };
            }
        }

        /// <summary>
        /// Verify OTP và tạo token để reset password
        /// </summary>
        public async Task<VerifyOTPResponse> VerifyOTPAndCreateResetTokenAsync(VerifyOTPRequest request)
        {
            try
            {
                // Tìm user theo email
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (user == null)
                {
                    return new VerifyOTPResponse
                    {
                        Success = false,
                        Message = "Email không tồn tại trong hệ thống"
                    };
                }

                // Verify OTP
                var isValidOTP = await _emailOTPService.VerifyOTPAsync(request.Email, request.OtpCode);
                
                if (!isValidOTP)
                {
                    return new VerifyOTPResponse
                    {
                        Success = false,
                        Message = "Mã OTP không hợp lệ hoặc đã hết hạn"
                    };
                }

                // Revoke tất cả token cũ của user này
                await RevokeAllTokensForUserAsync(user.UserId);

                // Tạo token mới để reset password (token này sẽ được dùng trong ResetPasswordAsync)
                var token = GenerateSecureToken();
                var expiresAt = DateTime.UtcNow.AddMinutes(15); // Token hết hạn sau 15 phút

                var passwordResetToken = new PasswordResetToken
                {
                    UserId = user.UserId,
                    Token = token,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IsRevoked = false
                };

                _db.PasswordResetTokens.Add(passwordResetToken);
                await _db.SaveChangesAsync();

                return new VerifyOTPResponse
                {
                    Success = true,
                    Message = "Xác thực OTP thành công. Bạn có thể đặt lại mật khẩu.",
                    ResetToken = token,
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verifying OTP and creating reset token: {ex.Message}");
                return new VerifyOTPResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi xác thực OTP"
                };
            }
        }

        /// <summary>
        /// Đặt lại mật khẩu bằng token
        /// </summary>
        public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                // Validate request
                if (request.NewPassword != request.ConfirmPassword)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Mật khẩu mới và xác nhận mật khẩu không khớp"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Mật khẩu phải có ít nhất 6 ký tự"
                    };
                }

                // Tìm và validate token
                var tokenEntity = await _db.PasswordResetTokens
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.Token == request.Token);

                if (tokenEntity == null)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Token không hợp lệ"
                    };
                }

                if (!IsTokenValid(tokenEntity))
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Token đã hết hạn hoặc đã được sử dụng"
                    };
                }

                // Hash mật khẩu mới
                var hashedPassword = HashPassword(request.NewPassword);

                // Cập nhật mật khẩu user
                tokenEntity.User.Password = hashedPassword;

                // Đánh dấu token đã được sử dụng
                tokenEntity.UsedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                // Gửi email thông báo thành công
                try
                {
                    await _emailService.SendPasswordResetSuccessEmailAsync(tokenEntity.User.Email, tokenEntity.User.Name ?? tokenEntity.User.Email);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"Error sending password reset success email: {emailEx.Message}");
                    // Không throw exception để không làm fail việc reset password
                }

                return new ResetPasswordResponse
                {
                    Success = true,
                    Message = "Đặt lại mật khẩu thành công"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting password: {ex.Message}");
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đặt lại mật khẩu"
                };
            }
        }

        /// <summary>
        /// Validate token
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenEntity = await _db.PasswordResetTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                return tokenEntity != null && IsTokenValid(tokenEntity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating token: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Revoke token
        /// </summary>
        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                var tokenEntity = await _db.PasswordResetTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (tokenEntity == null)
                    return false;

                tokenEntity.IsRevoked = true;
                await _db.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error revoking token: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lấy token theo giá trị
        /// </summary>
        public async Task<PasswordResetTokenDTO?> GetTokenByValueAsync(string token)
        {
            try
            {
                var tokenEntity = await _db.PasswordResetTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                return tokenEntity != null ? MapToDTO(tokenEntity) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting token by value: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy tất cả token của user
        /// </summary>
        public async Task<IEnumerable<PasswordResetTokenDTO>> GetTokensByUserIdAsync(int userId)
        {
            try
            {
                var tokens = await _db.PasswordResetTokens
                    .Where(t => t.UserId == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                return tokens.Select(MapToDTO);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tokens by user ID: {ex.Message}");
                return new List<PasswordResetTokenDTO>();
            }
        }

        /// <summary>
        /// Kiểm tra token có hợp lệ không
        /// </summary>
        public async Task<bool> IsTokenValidAsync(string token)
        {
            return await ValidateTokenAsync(token);
        }

        #region Private Methods

        /// <summary>
        /// Tạo token bảo mật
        /// </summary>
        private string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        /// <summary>
        /// Hash mật khẩu
        /// </summary>
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        /// <summary>
        /// Kiểm tra token có hợp lệ không
        /// </summary>
        private bool IsTokenValid(PasswordResetToken token)
        {
            return !token.IsRevoked &&
                   DateTime.UtcNow <= token.ExpiresAt &&
                   !token.UsedAt.HasValue;
        }

        /// <summary>
        /// Revoke tất cả token của user
        /// </summary>
        private async Task RevokeAllTokensForUserAsync(int userId)
        {
            var activeTokens = await _db.PasswordResetTokens
                .Where(t => t.UserId == userId && !t.IsRevoked && !t.UsedAt.HasValue)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Map entity to DTO
        /// </summary>
        private PasswordResetTokenDTO MapToDTO(PasswordResetToken entity)
        {
            return new PasswordResetTokenDTO
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Token = entity.Token,
                CreatedAt = entity.CreatedAt,
                ExpiresAt = entity.ExpiresAt,
                UsedAt = entity.UsedAt,
                IsRevoked = entity.IsRevoked
            };
        }

        #endregion
    }
}