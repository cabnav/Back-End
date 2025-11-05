using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Request để bắt đầu phiên sạc walk-in cho driver đã có tài khoản (không có đặt chỗ)
    /// </summary>
    public class WalkInSessionStartRequest
    {
        /// <summary>
        /// ID của điểm sạc (optional nếu có PointQrCode)
        /// </summary>
        public int? ChargingPointId { get; set; }

        /// <summary>
        /// Mã QR của điểm sạc (ví dụ: "POINT-15") - Bắt buộc nếu không có ChargingPointId
        /// </summary>
        public string? PointQrCode { get; set; }

        /// <summary>
        /// Phần trăm pin ban đầu (0-100)
        /// </summary>
        [Required(ErrorMessage = "Initial SOC is required")]
        [Range(0, 100, ErrorMessage = "Initial SOC must be between 0 and 100")]
        public int InitialSOC { get; set; }

        /// <summary>
        /// Ghi chú (optional)
        /// </summary>
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
    }
}
