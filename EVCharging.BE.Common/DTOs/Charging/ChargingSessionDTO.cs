using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Charging
{
    public class ChargingSessionDTO
    {
        public int SessionId { get; set; }
        public int DriverId { get; set; }
        public int PointId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int InitialSoc { get; set; }
        public int? FinalSoc { get; set; }
        public decimal EnergyUsed { get; set; }
        public int DurationMinutes { get; set; }
        public decimal CostBeforeDiscount { get; set; }
        public decimal AppliedDiscount { get; set; }
        public decimal FinalCost { get; set; }
        public string Status { get; set; }
        public ChargingPointDTO ChargingPoint { get; set; }
        public UserDTO Driver { get; set; }
        public List<SessionLogDTO> SessionLogs { get; set; }
    }
}
