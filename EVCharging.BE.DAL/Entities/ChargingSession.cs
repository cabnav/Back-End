using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class ChargingSession
{
    public int SessionId { get; set; }

    public int DriverId { get; set; }

    public int PointId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int InitialSoc { get; set; }

    public int? CurrentSoc { get; set; }

    public int? FinalSoc { get; set; }

    public decimal? EnergyUsed { get; set; }

    public int? DurationMinutes { get; set; }

    public decimal? CostBeforeDiscount { get; set; }

    public decimal? AppliedDiscount { get; set; }

    public decimal? DepositAmount { get; set; }

    public decimal? FinalCost { get; set; }

    public string? Status { get; set; }

    public int? ReservationId { get; set; }

    public string? Notes { get; set; }

    public virtual DriverProfile Driver { get; set; } = null!;

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ChargingPoint Point { get; set; } = null!;

    public virtual Reservation? Reservation { get; set; }

    public virtual ICollection<SessionLog> SessionLogs { get; set; } = new List<SessionLog>();
}
