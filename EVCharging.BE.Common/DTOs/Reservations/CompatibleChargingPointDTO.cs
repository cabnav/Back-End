using System;

namespace EVCharging.BE.Common.DTOs.Reservations
{
    /// <summary>
    /// Thông tin điểm sạc phù hợp với xe
    /// </summary>
    public class CompatibleChargingPointDTO
    {
        public int PointId { get; set; }
        public int StationId { get; set; }
        public string ConnectorType { get; set; } = null!;
        public int PowerOutput { get; set; }
        public decimal PricePerKwh { get; set; }
        public string Status { get; set; } = null!;
        public string StationName { get; set; } = null!;
        public string StationAddress { get; set; } = null!;
    }
}
