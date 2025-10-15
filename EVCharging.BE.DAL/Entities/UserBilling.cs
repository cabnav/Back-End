using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class UserBilling
{
    public int UserBillingId { get; set; }

    public int UserId { get; set; }

    public int PlanId { get; set; }

    public decimal? CurrentBalance { get; set; }

    public decimal? CreditUsed { get; set; }

    public string? Status { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime? NextBillingDate { get; set; }

    public virtual BillingPlan Plan { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
