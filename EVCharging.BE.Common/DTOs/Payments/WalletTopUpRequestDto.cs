using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Payments
{
    public class WalletTopUpRequestDto
    {
        [Required] public int UserId { get; set; }

        [Required, Range(1, 1_000_000_000, ErrorMessage = "Amount must be >= 1")]
        public decimal Amount { get; set; }

        // optional cho tương lai, hiện MockPay dùng "mock"
        public string PaymentMethod { get; set; } = "mock";
    }
}

