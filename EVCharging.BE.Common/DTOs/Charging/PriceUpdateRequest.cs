using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Request để cập nhật giá sạc
    /// </summary>
    public class PriceUpdateRequest
    {
        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 100000, ErrorMessage = "Price must be between 0.01 and 100,000 VND per kWh")]
        public decimal NewPrice { get; set; }

        [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? Reason { get; set; }

        public bool NotifyUsers { get; set; } = true;
    }
}
