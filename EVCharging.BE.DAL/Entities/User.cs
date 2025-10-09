using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Phone { get; set; }

    public string Role { get; set; } = null!;

    public decimal? WalletBalance { get; set; }

    public string? BillingType { get; set; }

    public string? MembershipTier { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<CorporateAccount> CorporateAccounts { get; set; } = new List<CorporateAccount>();

    public virtual DriverProfile? DriverProfile { get; set; }

    public virtual ICollection<IncidentReport> IncidentReportReporters { get; set; } = new List<IncidentReport>();

    public virtual ICollection<IncidentReport> IncidentReportResolvedByNavigations { get; set; } = new List<IncidentReport>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<StationStaff> StationStaffs { get; set; } = new List<StationStaff>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public virtual ICollection<UsageAnalytic> UsageAnalytics { get; set; } = new List<UsageAnalytic>();

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
