using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class ChargingPoint
{
    public int PointId { get; set; }

    public int StationId { get; set; }

    public string? ConnectorType { get; set; }

    public int? PowerOutput { get; set; }

    public decimal PricePerKwh { get; set; }

    public string? Status { get; set; }

    public string? QrCode { get; set; }

    public double? CurrentPower { get; set; }

    public DateOnly? LastMaintenance { get; set; }

    public virtual ICollection<ChargingSession> ChargingSessions { get; set; } = new List<ChargingSession>();

    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual ChargingStation Station { get; set; } = null!;
}
