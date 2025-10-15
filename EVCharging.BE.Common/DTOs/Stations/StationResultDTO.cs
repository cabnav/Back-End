namespace EVCharging.BE.Common.DTOs.Stations
{
    public class StationResultDTO
    {
        public int StationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Operator { get; set; }
        public string? Status { get; set; }
        public double DistanceKm { get; set; }
        public string GoogleMapsUrl { get; set; } = string.Empty;
    }
}
