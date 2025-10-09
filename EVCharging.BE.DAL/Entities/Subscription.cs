using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class Subscription
{
    public int SubscriptionId { get; set; }

    public int? UserId { get; set; }

    public int? CorporateId { get; set; }

    public int PlanId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string? Status { get; set; }

    public bool? AutoRenew { get; set; }

    public virtual CorporateAccount? Corporate { get; set; }

    public virtual PricingPlan Plan { get; set; } = null!;

    public virtual User? User { get; set; }
}
