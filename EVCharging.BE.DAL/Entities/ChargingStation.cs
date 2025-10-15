using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities
{
    public partial class ChargingStation
    {
        public int StationId { get; set; }

        public string Name { get; set; } = null!;

        public string Address { get; set; } = null!;

        // ✅ Thêm dòng này
        public string? City { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? Operator { get; set; }

        public string? Status { get; set; }

        public int? TotalPoints { get; set; }

        public int? AvailablePoints { get; set; }

        public virtual ICollection<ChargingPoint> ChargingPoints { get; set; } = new List<ChargingPoint>();

        public virtual ICollection<StationStaff> StationStaffs { get; set; } = new List<StationStaff>();

        public virtual ICollection<UsageAnalytic> UsageAnalyticFavoriteStations { get; set; } = new List<UsageAnalytic>();

        public virtual ICollection<UsageAnalytic> UsageAnalyticStations { get; set; } = new List<UsageAnalytic>();
    }
}
