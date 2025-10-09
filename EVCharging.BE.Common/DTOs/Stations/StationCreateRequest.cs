using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Stations
{
    public class StationCreateRequest
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Operator { get; set; }
        public List<ChargingPointCreateRequest> ChargingPoints { get; set; }
    }

    public class ChargingPointCreateRequest
    {
        public string ConnectorType { get; set; }
        public int PowerOutput { get; set; }
        public decimal PricePerKwh { get; set; }
        public string QrCode { get; set; }
    }
}
