using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class InvoiceItem
{
    public int ItemId { get; set; }

    public int InvoiceId { get; set; }

    public int? SessionId { get; set; }

    public string? Description { get; set; }

    public decimal? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal? Amount { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual ChargingSession? Session { get; set; }
}
