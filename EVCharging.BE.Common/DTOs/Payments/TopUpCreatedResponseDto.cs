using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Payments
{
    public class TopUpCreatedResponseDto
    {
        public string Code { get; set; } = default!;
        public string CheckoutUrl { get; set; } = default!;
        public string QrImageUrl { get; set; } = default!;
        public string QrBase64 { get; set; } = default!;
        public DateTime ExpiresAtUtc { get; set; }
    }
}
