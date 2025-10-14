using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class DriverProfile
{
    public int DriverId { get; set; }

    public int UserId { get; set; }

    public string? LicenseNumber { get; set; }

    public string? VehicleModel { get; set; }

    public string? VehiclePlate { get; set; }

    public int? BatteryCapacity { get; set; }

    public int? CorporateId { get; set; }

    public virtual ICollection<ChargingSession> ChargingSessions { get; set; } = new List<ChargingSession>();

    public virtual CorporateAccount? Corporate { get; set; }

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

   public virtual User User { get; set; } = null!;
                                     
}
