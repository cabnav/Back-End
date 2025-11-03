using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Auth
{
    /// <summary>
    /// Request to send OTP to email for verification
    /// </summary>
    public class SendOTPRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = null!;

        public string? Purpose { get; set; } = "registration";
    }

    /// <summary>
    /// Request to verify OTP code
    /// </summary>
    public class VerifyOTPRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "OTP code is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be 6 digits")]
        public string OtpCode { get; set; } = null!;
    }
}

