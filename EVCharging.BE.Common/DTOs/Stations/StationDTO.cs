using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Stations
{
    public class StationDTO
    {
        public int StationId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Operator { get; set; }
        public string Status { get; set; }
        public int TotalPoints { get; set; }
        public int AvailablePoints { get; set; }
        public List<ChargingPointDTO> ChargingPoints { get; set; }
        public double Distance { get; set; } // Distance from user in km
    }
}
