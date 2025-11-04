using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// Request để staff cập nhật trạng thái thanh toán (từ pending sang success)
    /// </summary>
    public class UpdatePaymentStatusRequest
    {
        [Required(ErrorMessage = "Status is required")]
        [RegularExpression("^(success|completed|failed)$", ErrorMessage = "Status must be 'success', 'completed', or 'failed'")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Transaction ID (optional, null cho tiền mặt)
        /// </summary>
        [StringLength(100, ErrorMessage = "Transaction ID cannot exceed 100 characters")]
        public string? TransactionId { get; set; }

        /// <summary>
        /// Ghi chú của staff (optional)
        /// </summary>
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
    }
}

