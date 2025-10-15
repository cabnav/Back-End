using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class BillingPlan
{
    public int PlanId { get; set; }

    public string PlanName { get; set; } = null!;

    public decimal? SubscriptionFee { get; set; }

    public string? BillingCycle { get; set; }

    public string? PaymentTerms { get; set; }

    public decimal? CreditLimit { get; set; }

    public virtual ICollection<UserBilling> UserBillings { get; set; } = new List<UserBilling>();
}
