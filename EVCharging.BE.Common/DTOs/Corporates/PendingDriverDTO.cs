using System;

namespace EVCharging.BE.Common.DTOs.Corporates
{
    public class PendingDriverDTO
    {
        public int DriverId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string? UserEmail { get; set; }
        public string? UserPhone { get; set; }
        public string? LicenseNumber { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehiclePlate { get; set; }
        public int? BatteryCapacity { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}

