using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Stations
{
    public class ChargingPointDTO
    {
        public int PointId { get; set; }
        public int StationId { get; set; }
        public string ConnectorType { get; set; }
        public int PowerOutput { get; set; }
        public decimal PricePerKwh { get; set; }
        public string Status { get; set; }
        public string QrCode { get; set; }
        public decimal CurrentPower { get; set; }
        public DateTime? LastMaintenance { get; set; }
        public bool IsAvailable => Status == "available";
    }
}
