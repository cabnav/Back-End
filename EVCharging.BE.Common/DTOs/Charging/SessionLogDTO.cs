using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Charging
{
    public class SessionLogDTO
    {
        public int LogId { get; set; }
        public int SocPercentage { get; set; }
        public decimal CurrentPower { get; set; }
        public decimal Voltage { get; set; }
        public decimal Temperature { get; set; }
        public DateTime LogTime { get; set; }
    }
}
