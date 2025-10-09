using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public int? UserId { get; set; }

    public int? CorporateId { get; set; }

    public DateOnly BillingPeriodStart { get; set; }

    public DateOnly BillingPeriodEnd { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Status { get; set; }

    public DateOnly DueDate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual CorporateAccount? Corporate { get; set; }

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    public virtual User? User { get; set; }
}
