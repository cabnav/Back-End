using EVCharging.BE.Common.DTOs.Auth;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace EVCharging.BE.Services.Services.Implementation
{
    /// <summary>
    /// Service quản lý đặt lại mật khẩu
    /// </summary>
    public class PasswordResetService : IPasswordResetService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public PasswordResetService(EvchargingManagementContext db, IConfiguration configuration, IEmailService emailService)
        {
            _db = db;
            _configuration = configuration;
            _emailService = emailService;
        }

        /// <summary>
        /// Tạo token đặt lại mật khẩu
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

                // Revoke tất cả token cũ của user này
                await RevokeAllTokensForUserAsync(user.UserId);

                // Tạo token mới
                var token = GenerateSecureToken();
                var expiresAt = DateTime.UtcNow.AddHours(1); // Token hết hạn sau 1 giờ

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

                // Gửi email chứa token
                try
                {
                    var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:7035";
                    var resetUrl = $"{baseUrl}/reset-password?token={token}";
                    await _emailService.SendPasswordResetEmailAsync(user.Email, user.Name ?? user.Email, token, resetUrl);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"Error sending password reset email: {emailEx.Message}");
                    // Không throw exception để không làm fail việc tạo token
                }

                return new CreatePasswordResetTokenResponse
                {
                    Success = true,
                    Message = "Email đặt lại mật khẩu đã được gửi thành công",
                    Token = _configuration["Environment"] == "Development" ? token : null, // Chỉ hiển thị token trong development
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating password reset token: {ex.Message}");
                return new CreatePasswordResetTokenResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi tạo token đặt lại mật khẩu"
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