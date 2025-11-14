using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class IncidentReport
{
    public int ReportId { get; set; }

    public int ReporterId { get; set; }

    public int PointId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? Priority { get; set; }

    public string? Status { get; set; }

    public DateTime? ReportedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int? ResolvedBy { get; set; }

    public string? AdminNotes { get; set; }

    public virtual ChargingPoint Point { get; set; } = null!;

    public virtual User Reporter { get; set; } = null!;

    public virtual User? ResolvedByNavigation { get; set; }
}
