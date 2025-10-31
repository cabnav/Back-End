using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Request để staff dừng khẩn cấp phiên sạc
    /// </summary>
    public class EmergencyStopRequest
    {
        [Required(ErrorMessage = "Reason for emergency stop is required")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Reason must be between 10 and 500 characters")]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Điểm sạc có cần bảo trì không
        /// </summary>
        public bool RequiresMaintenance { get; set; } = true;

        /// <summary>
        /// Có thông báo cho Admin ngay lập tức không
        /// </summary>
        public bool NotifyAdmin { get; set; } = true;

        /// <summary>
        /// Mức độ nghiêm trọng: low, medium, high, critical
        /// </summary>
        [RegularExpression("^(low|medium|high|critical)$", ErrorMessage = "Severity must be 'low', 'medium', 'high', or 'critical'")]
        public string Severity { get; set; } = "high";

        /// <summary>
        /// Đính kèm ảnh chứng cứ (URLs)
        /// </summary>
        public List<string>? PhotoUrls { get; set; }
    }
}

