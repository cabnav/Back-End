using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Shared
{
    /// <summary>
    /// Request để cập nhật trạng thái báo cáo sự cố
    /// </summary>
    public class UpdateIncidentStatusRequest
    {
        /// <summary>
        /// Trạng thái mới: open, in_progress, resolved
        /// </summary>
        [Required(ErrorMessage = "Status is required")]
        [RegularExpression("^(open|in_progress|resolved)$", ErrorMessage = "Status must be 'open', 'in_progress', or 'resolved'")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Ghi chú/Phản hồi từ admin (optional)
        /// </summary>
        public string? Notes { get; set; }
    }
}

