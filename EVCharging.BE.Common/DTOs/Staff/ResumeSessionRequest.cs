using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Request để tiếp tục phiên sạc đã tạm dừng
    /// </summary>
    public class ResumeSessionRequest
    {
        [StringLength(300, ErrorMessage = "Notes cannot exceed 300 characters")]
        public string? Notes { get; set; }
    }
}

