using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class PricingPlan
{
    public int PlanId { get; set; }

    public string Name { get; set; } = null!;

    public string? PlanType { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? BillingCycle { get; set; }

    public decimal? DiscountRate { get; set; }

    public string? TargetAudience { get; set; }

    public string? Benefits { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
