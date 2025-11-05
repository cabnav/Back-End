using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Request để bắt đầu phiên sạc
    /// </summary>
    public class ChargingSessionStartRequest
    {
        // ✅ Nếu có pointQrCode, không cần ChargingPointId (sẽ tự động lookup)
        // Nếu không có pointQrCode, phải có ChargingPointId
        public int? ChargingPointId { get; set; }

        [Required(ErrorMessage = "Driver ID is required")]
        public int DriverId { get; set; }

        [Required(ErrorMessage = "Initial SOC is required")]
        [Range(0, 100, ErrorMessage = "Initial SOC must be between 0 and 100")]
        public int InitialSOC { get; set; }

        // ✅ Mã QR của điểm sạc (ví dụ: "POINT-15") - dùng cho check-in
        public string? PointQrCode { get; set; }

        // ✅ Mã QR cũ (deprecated, giữ lại để tương thích)
        [Obsolete("Use PointQrCode instead")]
        public string? QrCode { get; set; }

        public string? Notes { get; set; }

        // Optional: cho phép set thời điểm bắt đầu (UTC) – dùng cho check-in theo reservation
        public DateTime? StartAtUtc { get; set; }

        // ✅ Mã reservation - bắt buộc khi check-in từ reservation
        public string? ReservationCode { get; set; }

        /// <summary>
        /// Thời gian tối đa kết thúc session (UTC) - dùng cho walk-in session khi có reservation sắp đến
        /// </summary>
        public DateTime? MaxEndTimeUtc { get; set; }
    }
}
