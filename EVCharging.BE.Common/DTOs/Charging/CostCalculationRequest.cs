using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Request để tính toán chi phí sạc
    /// </summary>
    public class CostCalculationRequest
    {
        [Required(ErrorMessage = "Charging Point ID is required")]
        public int ChargingPointId { get; set; }

        [Required(ErrorMessage = "Energy used is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Energy used must be positive")]
        public decimal EnergyUsed { get; set; }

        [Required(ErrorMessage = "Duration is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Duration must be positive")]
        public int DurationMinutes { get; set; }

        public int? UserId { get; set; }
        public string? MembershipTier { get; set; }
        public bool IsPeakHours { get; set; } = false;
        public decimal? CustomDiscountRate { get; set; }
        
        /// <summary>
        /// Thời điểm bắt đầu session (để tính phụ thu giờ cao điểm chính xác)
        /// Nếu không có, sẽ dùng DateTime.UtcNow (không chính xác)
        /// </summary>
        public DateTime? StartTime { get; set; }
    }
}
