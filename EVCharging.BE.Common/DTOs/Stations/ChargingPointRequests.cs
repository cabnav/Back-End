using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Stations
{
    public class ChargingPointCreateRequest
    {
        [Required] public int StationId { get; set; }
        public string? ConnectorType { get; set; }
        public string? Status { get; set; } = "Available";
        public int? PowerOutput { get; set; }
        public decimal PricePerKwh { get; set; } = 0;
        public double? CurrentPower { get; set; }
        public DateOnly? LastMaintenance { get; set; }
        public string? QrCode { get; set; }
    }

    public class ChargingPointUpdateRequest
    {
        public string? ConnectorType { get; set; }
        public string? Status { get; set; }
        public int? PowerOutput { get; set; }
        public decimal? PricePerKwh { get; set; }
        public double? CurrentPower { get; set; }
        public DateOnly? LastMaintenance { get; set; }
        public string? QrCode { get; set; }
    }
}
