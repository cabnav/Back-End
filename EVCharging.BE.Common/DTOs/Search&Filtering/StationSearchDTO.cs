namespace EVCharging.BE.Services.DTOs
{
    public class StationSearchDTO
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Operator { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? MaxDistanceKm { get; set; }
    }
}
