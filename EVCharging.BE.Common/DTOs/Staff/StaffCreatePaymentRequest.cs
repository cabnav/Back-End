using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Request để Staff tạo payment record cho session đã hoàn thành (thanh toán tại trạm)
    /// </summary>
    public class StaffCreatePaymentRequest
    {
        [Required(ErrorMessage = "Payment method is required")]
        [RegularExpression("^(cash|card|pos)$", ErrorMessage = "Payment method must be 'cash', 'card', or 'pos'")]
        public string PaymentMethod { get; set; } = "cash";

        /// <summary>
        /// Ghi chú của staff (optional)
        /// </summary>
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
    }
}

