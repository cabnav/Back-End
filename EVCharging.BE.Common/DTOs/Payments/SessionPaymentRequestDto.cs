using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// DTO để thanh toán phiên sạc bằng SessionId
    /// </summary>
    public class SessionPaymentRequestDto
    {
        [Required(ErrorMessage = "SessionId is required")]
        public int SessionId { get; set; }
    }
}

