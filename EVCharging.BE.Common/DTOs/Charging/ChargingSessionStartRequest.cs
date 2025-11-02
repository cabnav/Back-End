using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Request để bắt đầu phiên sạc
    /// </summary>
    public class ChargingSessionStartRequest
    {
        [Required(ErrorMessage = "Charging Point ID is required")]
        public int ChargingPointId { get; set; }

        [Required(ErrorMessage = "Driver ID is required")]
        public int DriverId { get; set; }

        [Required(ErrorMessage = "Initial SOC is required")]
        [Range(0, 100, ErrorMessage = "Initial SOC must be between 0 and 100")]
        public int InitialSOC { get; set; }

        [Required(ErrorMessage = "QR Code is required")]
        public string QrCode { get; set; } = string.Empty;

        public string? Notes { get; set; }

        // Optional: cho phép set thời điểm bắt đầu (UTC) – dùng cho check-in theo reservation
        public DateTime? StartAtUtc { get; set; }

        // Optional: gắn mã reservation phục vụ trace/audit
        public string? ReservationCode { get; set; }
    }
}
