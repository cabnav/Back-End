using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Charging
{
    public class SessionStartRequest
    {
        public int PointId { get; set; }
        public int InitialSoc { get; set; }
        public string QrCode { get; set; } // For validation
    }
}
