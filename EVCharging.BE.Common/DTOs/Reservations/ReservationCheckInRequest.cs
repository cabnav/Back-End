using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Reservations
{
    /// <summary>
    /// Request để check-in reservation
    /// </summary>
    public class ReservationCheckInRequest
    {
        /// <summary>
        /// Mã QR của điểm sạc (ví dụ: "POINT-15") - Bắt buộc để xác nhận đúng cột sạc
        /// </summary>
        [Required(ErrorMessage = "Point QR Code is required")]
        public string PointQrCode { get; set; } = string.Empty;

        /// <summary>
        /// Phần trăm pin hiện tại (0-100)
        /// </summary>
        [Required(ErrorMessage = "Initial SOC is required")]
        [Range(0, 100, ErrorMessage = "Initial SOC must be between 0 and 100")]
        public int InitialSOC { get; set; } = 10;
    }
}

