using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Auth
{
    /// <summary>
    /// DTO cho PasswordResetToken response
    /// </summary>
    public class PasswordResetTokenDTO
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public bool IsRevoked { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        public bool IsUsed => UsedAt.HasValue;
        public bool IsValid => !IsRevoked && !IsExpired && !IsUsed;
    }

    /// <summary>
    /// DTO cho request tạo password reset token
    /// </summary>
    public class CreatePasswordResetTokenRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO cho request reset password
    /// </summary>
    public class ResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO cho response tạo password reset token
    /// </summary>
    public class CreatePasswordResetTokenResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; } // Chỉ trả về trong development
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// DTO cho response reset password
    /// </summary>
    public class ResetPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
