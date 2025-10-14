namespace EVCharging.BE.Services.DTOs
{
    public class StationResultDTO
    {
        public int StationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string GoogleMapsUrl =>
            $"https://www.google.com/maps?q={Latitude},{Longitude}";
    }
}
