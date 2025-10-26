using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Payments
{
    public class WalletDebitRequestDto
    {
        [Required] public int UserId { get; set; }
        [Required, Range(1, 1_000_000_000)] public decimal Amount { get; set; }
        public string? Description { get; set; }
        public int? ReferenceId { get; set; }
    }
}
