using EVCharging.BE.Common.Enums;

namespace EVCharging.BE.Common.DTOs.Stations
{
    /// <summary>
    /// DTO for interactive charging station map with real-time status
    /// </summary>
    public class InteractiveStationDTO
    {
        public int StationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? City { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Operator { get; set; }
        public string Status { get; set; } = string.Empty;
        public double DistanceKm { get; set; }
        public string GoogleMapsUrl { get; set; } = string.Empty;
        
        // Real-time status information
        public int TotalPoints { get; set; }
        public int AvailablePoints { get; set; }
        public int BusyPoints { get; set; }
        public int MaintenancePoints { get; set; }
        public double UtilizationPercentage { get; set; }
        
        // Charging points with connector types
        public List<ChargingPointMapDTO> ChargingPoints { get; set; } = new();
        
        // Pricing information
        public PricingInfoDTO Pricing { get; set; } = new();
    }

    /// <summary>
    /// DTO for charging point information on map
    /// </summary>
    public class ChargingPointMapDTO
    {
        public int PointId { get; set; }
        public ConnectorType ConnectorType { get; set; }
        public int PowerOutput { get; set; }
        public decimal PricePerKwh { get; set; }
        public string Status { get; set; } = string.Empty;
        public double CurrentPower { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime? LastMaintenance { get; set; }
    }

    /// <summary>
    /// DTO for pricing information including time-based rates
    /// </summary>
    public class PricingInfoDTO
    {
        public decimal BasePricePerKwh { get; set; }
        public decimal PeakHourPrice { get; set; }
        public decimal OffPeakPrice { get; set; }
        public string PeakHours { get; set; } = string.Empty; // e.g., "18:00-22:00"
        public decimal CurrentPrice { get; set; }
        public bool IsPeakHour { get; set; }
        public string PriceDescription { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for station filtering
    /// </summary>
    public class StationFilterDTO
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Operator { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? MaxDistanceKm { get; set; }
        public List<ConnectorType>? ConnectorTypes { get; set; }
        public int? MinPowerOutput { get; set; }
        public decimal? MaxPricePerKwh { get; set; }
        public bool? IsAvailable { get; set; }
        public string? Status { get; set; }
    }
}
