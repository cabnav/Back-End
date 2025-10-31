using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Request để staff khởi động phiên sạc cho khách walk-in (không có app)
    /// </summary>
    public class WalkInSessionRequest
    {
        [Required(ErrorMessage = "Charging point ID is required")]
        public int ChargingPointId { get; set; }

        [Required(ErrorMessage = "Customer name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Customer name must be between 2 and 100 characters")]
        public string CustomerName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? CustomerPhone { get; set; }

        [Required(ErrorMessage = "Vehicle plate is required")]
        [StringLength(20, MinimumLength = 5, ErrorMessage = "Vehicle plate must be between 5 and 20 characters")]
        public string VehiclePlate { get; set; } = string.Empty;

        [Range(0, 100, ErrorMessage = "Initial SOC must be between 0 and 100")]
        public int InitialSOC { get; set; } = 0;

        [Range(1, 100, ErrorMessage = "Target SOC must be between 1 and 100")]
        public int TargetSOC { get; set; } = 80;

        [Required(ErrorMessage = "Payment method is required")]
        [RegularExpression("^(cash|card|pos)$", ErrorMessage = "Payment method must be 'cash', 'card', or 'pos'")]
        public string PaymentMethod { get; set; } = "cash";

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }

        /// <summary>
        /// Thông tin xe (optional)
        /// </summary>
        [StringLength(100, ErrorMessage = "Vehicle model cannot exceed 100 characters")]
        public string? VehicleModel { get; set; }

        /// <summary>
        /// Dung lượng pin (kWh)
        /// </summary>
        [Range(0, 200, ErrorMessage = "Battery capacity must be between 0 and 200 kWh")]
        public decimal? BatteryCapacity { get; set; }
    }
}

