using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.DriverProfiles
{
    public class DriverProfileCreateRequest
    {
        public int UserId { get; set; }               
        public string? LicenseNumber { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehiclePlate { get; set; }
        public int? BatteryCapacity { get; set; }
        public int? CorporateId { get; set; }
    }

    public class DriverProfileUpdateRequest
    {
        public string? LicenseNumber { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehiclePlate { get; set; }
        public int? BatteryCapacity { get; set; }
        public int? CorporateId { get; set; }
    }
}
