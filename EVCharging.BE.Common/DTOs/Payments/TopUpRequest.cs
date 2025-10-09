using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Payments
{
    public class TopUpRequest
    {
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } // "credit_card", "bank_transfer"
    }
}
