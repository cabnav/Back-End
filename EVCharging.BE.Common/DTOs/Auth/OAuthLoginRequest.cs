using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Auth
{
    /// <summary>
    /// Request for OAuth login/register (Google, Facebook, etc.)
    /// </summary>
    public class OAuthLoginRequest
    {
        [Required]
        public string Provider { get; set; } = null!; // "google" or "facebook"
        
        [Required]
        public string ProviderId { get; set; } = null!; // External provider user ID
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        
        [Required]
        public string Name { get; set; } = null!;
        
        public string? Phone { get; set; }
        
        public string? Role { get; set; } = "driver";
        
        // Driver specific (optional)
        public string? LicenseNumber { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehiclePlate { get; set; }
        public int? BatteryCapacity { get; set; }
    }
}

