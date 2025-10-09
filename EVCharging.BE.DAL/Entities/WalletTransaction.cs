using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class WalletTransaction
{
    public int TransactionId { get; set; }

    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public string? TransactionType { get; set; }

    public string? Description { get; set; }

    public decimal BalanceAfter { get; set; }

    public int? ReferenceId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
