using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Payments
{
    public class PaymentRequest
    {
        public int SessionId { get; set; }
        public string PaymentMethod { get; set; } // "wallet", "credit_card", "corporate_billing"
    }
}
