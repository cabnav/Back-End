namespace EVCharging.BE.Common.DTOs.Stations
{
    public class ChargingPointDTO
    {
        public int PointId { get; set; }
        public int StationId { get; set; }
        public string? ConnectorType { get; set; }
        public string? Status { get; set; }
        public int? PowerOutput { get; set; }
        public decimal PricePerKwh { get; set; }
        public double? CurrentPower { get; set; }
        public DateOnly? LastMaintenance { get; set; }
        public string? QrCode { get; set; }

        public string? StationName { get; set; }
        public string? StationAddress { get; set; }
    }
}
