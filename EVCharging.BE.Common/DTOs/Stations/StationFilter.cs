using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Stations
{
    public class StationFilter
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public decimal? Radius { get; set; } // in km
        public string ConnectorType { get; set; }
        public int? MinPower { get; set; }
        public decimal? MaxPrice { get; set; }
        public string Status { get; set; } = "active";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
