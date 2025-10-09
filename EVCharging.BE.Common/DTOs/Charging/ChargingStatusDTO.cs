using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Charging
{
    public class ChargingStatusDTO
    {
        public int SessionId { get; set; }
        public int CurrentSoc { get; set; }
        public decimal CurrentPower { get; set; }
        public decimal EnergyUsed { get; set; }
        public decimal CurrentCost { get; set; }
        public int EstimatedRemainingMinutes { get; set; }
        public DateTime StartTime { get; set; }
        public string Status { get; set; }
    }
