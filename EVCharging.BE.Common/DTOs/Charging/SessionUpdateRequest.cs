using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Charging
{
    public class SessionUpdateRequest
    {
        public int SessionId { get; set; }
        public int? FinalSoc { get; set; }
        public string Status { get; set; } // "completed", "interrupted"
    }
}
