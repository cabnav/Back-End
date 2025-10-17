using EVCharging.BE.Common.DTOs.Auth;

namespace EVCharging.BE.Services.Services
{
    /// <summary>
    /// Interface cho Password Reset Service - quản lý đặt lại mật khẩu
    /// </summary>
    public interface IPasswordResetService
    {
        // Token Management
        Task<CreatePasswordResetTokenResponse> CreatePasswordResetTokenAsync(CreatePasswordResetTokenRequest request);
        Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request);
        Task<bool> ValidateTokenAsync(string token);
        Task<bool> RevokeTokenAsync(string token);

        // Token Queries
        Task<PasswordResetTokenDTO?> GetTokenByValueAsync(string token);
        Task<IEnumerable<PasswordResetTokenDTO>> GetTokensByUserIdAsync(int userId);
        Task<bool> IsTokenValidAsync(string token);
    }
}