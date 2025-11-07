namespace EVCharging.BE.Common.DTOs.Stations
{
    // EVCharging.BE.Common/DTOs/Stations/StationCreateRequest.cs
    using System.ComponentModel.DataAnnotations;

    namespace EVCharging.BE.Common.DTOs.Stations
    {
        public class StationCreateRequest
        {
            [Required] public string Name { get; set; } = "";
            [Required] public string Address { get; set; } = "";
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }
            public string? Operator { get; set; }
            public string? Status { get; set; } = "Active";
        }
    }


    // EVCharging.BE.Common/DTOs/Stations/StationUpdateRequest.cs
    namespace EVCharging.BE.Common.DTOs.Stations
    {
        public class StationUpdateRequest
        {
            public string? Name { get; set; }
            public string? Address { get; set; }
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }
            public string? Operator { get; set; }
            public string? Status { get; set; }
        }
       
    }

}
