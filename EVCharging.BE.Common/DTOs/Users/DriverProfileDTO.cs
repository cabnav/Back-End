﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Users
{
    public class DriverProfileDTO
    {
        public int DriverId { get; set; }
        public string LicenseNumber { get; set; }
        public string VehicleModel { get; set; }
        public string VehiclePlate { get; set; }
        public int? BatteryCapacity { get; set; }
        //public CorporateAccountDTO CorporateAccount { get; set; }
    }
}
