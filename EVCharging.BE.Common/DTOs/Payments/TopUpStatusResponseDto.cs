using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Payments
{
    public class TopUpStatusResponseDto
    {
        public string Code { get; set; } = default!;
        public string Status { get; set; } = default!;
    }
}

