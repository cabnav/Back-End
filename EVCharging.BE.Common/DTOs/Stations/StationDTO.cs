// EVCharging.BE.Common/DTOs/Stations/StationDTO.cs
namespace EVCharging.BE.Common.DTOs.Stations
{
    public class StationDTO
    {
        public int StationId { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Operator { get; set; }
        public string? Status { get; set; }
        public int? TotalPoints { get; set; }
        public int? AvailablePoints { get; set; }
    }
}
