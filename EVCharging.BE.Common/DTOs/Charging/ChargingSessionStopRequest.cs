using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Request để dừng phiên sạc
    /// </summary>
    public class ChargingSessionStopRequest
    {
        [Required(ErrorMessage = "Session ID is required")]
        public int SessionId { get; set; }

        [Required(ErrorMessage = "Final SOC is required")]
        [Range(0, 100, ErrorMessage = "Final SOC must be between 0 and 100")]
        public int FinalSOC { get; set; }

        public string? Reason { get; set; }
    }
}
