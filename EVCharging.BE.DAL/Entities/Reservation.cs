using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class Reservation
{
    public int ReservationId { get; set; }

    public int DriverId { get; set; }

    public int PointId { get; set; }

    public string ReservationCode { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Target SOC (%) mà người dùng muốn sạc đến khi đặt reservation
    /// Nếu null, mặc định sẽ sạc đến 100%
    /// </summary>
    public int? TargetSoc { get; set; }

    public virtual ICollection<ChargingSession> ChargingSessions { get; set; } = new List<ChargingSession>();

    public virtual DriverProfile Driver { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ChargingPoint Point { get; set; } = null!;
}
