using System;

namespace EVCharging.BE.DAL.Entities;

public partial class Deposit
{
    public int DepositId { get; set; }

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

