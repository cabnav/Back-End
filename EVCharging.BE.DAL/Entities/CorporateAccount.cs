using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class CorporateAccount
{
    public int CorporateId { get; set; }

    public string CompanyName { get; set; } = null!;

    public string? TaxCode { get; set; }

    public string? ContactPerson { get; set; }

    public string? ContactEmail { get; set; }

    public string? BillingType { get; set; }

    public decimal? CreditLimit { get; set; }

    public int AdminUserId { get; set; }

    public string? Status { get; set; } 

    public DateTime? CreatedAt { get; set; }

    public virtual User AdminUser { get; set; } = null!;

    public virtual ICollection<DriverProfile> DriverProfiles { get; set; } = new List<DriverProfile>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
