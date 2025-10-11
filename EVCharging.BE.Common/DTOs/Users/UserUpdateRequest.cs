using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Users
{
    public class UserUpdateRequest
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string LicenseNumber { get; set; }
        public string VehicleModel { get; set; }
        public string VehiclePlate { get; set; }
        public int? BatteryCapacity { get; set; }
    }
}
