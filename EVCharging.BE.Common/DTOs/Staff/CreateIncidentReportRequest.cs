using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Request để staff tạo báo cáo sự cố
    /// </summary>
    public class CreateIncidentReportRequest
    {
        [Required(ErrorMessage = "Point ID is required")]
        public int PointId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 1000 characters")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Mức độ ưu tiên: low, medium, high, critical
        /// </summary>
        [RegularExpression("^(low|medium|high|critical)$", ErrorMessage = "Priority must be 'low', 'medium', 'high', or 'critical'")]
        public string Priority { get; set; } = "medium";

        /// <summary>
        /// Điểm sạc có cần bảo trì không
        /// </summary>
        public bool RequiresMaintenance { get; set; } = false;

        /// <summary>
        /// Có thông báo cho Admin ngay lập tức không
        /// </summary>
        public bool NotifyAdmin { get; set; } = true;

        /// <summary>
        /// Đính kèm ảnh chứng cứ (URLs)
        /// </summary>
        public List<string>? PhotoUrls { get; set; }
    }
}

