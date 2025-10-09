using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Payments
{
    public class WalletTransactionDTO
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; }
        public string Description { get; set; }
        public decimal BalanceAfter { get; set; }
        public int? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
