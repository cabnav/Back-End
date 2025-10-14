using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Request để tạo log cho phiên sạc
    /// </summary>
    public class SessionLogCreateRequest
    {
        [Required(ErrorMessage = "Session ID is required")]
        public int SessionId { get; set; }

        [Range(0, 100, ErrorMessage = "SOC percentage must be between 0 and 100")]
        public int? SOCPercentage { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Current power must be positive")]
        public decimal? CurrentPower { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Voltage must be positive")]
        public decimal? Voltage { get; set; }

        [Range(-50, 100, ErrorMessage = "Temperature must be between -50 and 100")]
        public decimal? Temperature { get; set; }

        public DateTime? LogTime { get; set; } = DateTime.UtcNow;
    }
}
