using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class StationStaff
{
    public int AssignmentId { get; set; }

    public int StaffId { get; set; }

    public int StationId { get; set; }

    public DateTime ShiftStart { get; set; }

    public DateTime ShiftEnd { get; set; }

    public string? Status { get; set; }

    public virtual User Staff { get; set; } = null!;

    public virtual ChargingStation Station { get; set; } = null!;
}
