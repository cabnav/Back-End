using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class SessionLog
{
    public int LogId { get; set; }

    public int SessionId { get; set; }

    public int? SocPercentage { get; set; }

    public decimal? CurrentPower { get; set; }

    public decimal? Voltage { get; set; }

    public decimal? Temperature { get; set; }

    public DateTime? LogTime { get; set; }

    public virtual ChargingSession Session { get; set; } = null!;
}
