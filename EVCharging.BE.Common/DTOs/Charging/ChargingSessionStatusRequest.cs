using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Request để cập nhật trạng thái phiên sạc
    /// </summary>
    public class ChargingSessionStatusRequest
    {
        [Required(ErrorMessage = "Session ID is required")]
        public int SessionId { get; set; }

        [Required(ErrorMessage = "Status is required")]
        public string Status { get; set; } = string.Empty;

        public int? CurrentSOC { get; set; }
        public decimal? CurrentPower { get; set; }
        public decimal? Voltage { get; set; }
        public decimal? Temperature { get; set; }
    }
}
