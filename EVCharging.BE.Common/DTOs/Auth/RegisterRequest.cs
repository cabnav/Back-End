using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Auth
{
    public class RegisterRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; } = "driver";

        // Driver specific
        public string LicenseNumber { get; set; }
        public string VehicleModel { get; set; }
        public string VehiclePlate { get; set; }
        public int? BatteryCapacity { get; set; }
    }
}
