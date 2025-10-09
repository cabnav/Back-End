using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class UsageAnalytic
{
    public int AnalyticsId { get; set; }

    public int UserId { get; set; }

    public int StationId { get; set; }

    public int? SessionCount { get; set; }

    public decimal? TotalEnergyUsed { get; set; }

    public decimal? TotalCost { get; set; }

    public int? FavoriteStationId { get; set; }

    public int? PeakUsageHour { get; set; }

    public DateOnly AnalysisMonth { get; set; }

    public virtual ChargingStation? FavoriteStation { get; set; }

    public virtual ChargingStation Station { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
