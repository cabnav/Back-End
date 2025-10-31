using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Request để tạm dừng phiên sạc
    /// </summary>
    public class PauseSessionRequest
    {
        [Required(ErrorMessage = "Reason for pausing is required")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 300 characters")]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Thời gian tạm dừng tối đa (phút). Sau thời gian này sẽ tự động hủy session
        /// </summary>
        [Range(1, 60, ErrorMessage = "Max pause duration must be between 1 and 60 minutes")]
        public int MaxPauseDuration { get; set; } = 15;
    }
}

