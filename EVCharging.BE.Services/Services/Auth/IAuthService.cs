using EVCharging.BE.Common.DTOs.Auth;

namespace EVCharging.BE.Services.Services.Auth
{
    public interface IAuthService
    {
        Task<AuthResponse?> LoginAsync(LoginRequest request);
        Task<AuthResponse?> RegisterAsync(RegisterRequest request);
        Task<bool> LogoutAsync(string token);
        Task<bool> ValidateTokenAsync(string token);
        Task<string> GenerateTokenAsync(int userId, string email, string role);
    }
}
