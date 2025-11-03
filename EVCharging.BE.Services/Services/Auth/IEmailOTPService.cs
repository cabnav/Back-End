namespace EVCharging.BE.Services.Services.Auth
{
    public interface IEmailOTPService
    {
        /// <summary>
        /// Generate and send OTP to email for verification
        /// </summary>
        Task<bool> SendOTPAsync(string email, string purpose = "registration");

        /// <summary>
        /// Verify OTP code
        /// </summary>
        Task<bool> VerifyOTPAsync(string email, string otpCode);

        /// <summary>
        /// Check if email has valid OTP (not expired and not used)
        /// </summary>
        Task<bool> HasValidOTPAsync(string email);

        /// <summary>
        /// Delete expired OTPs (cleanup job)
        /// </summary>
        Task<int> DeleteExpiredOTPsAsync();
    }
}

