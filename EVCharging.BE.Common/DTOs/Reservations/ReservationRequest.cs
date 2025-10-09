using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Reservations
{
    public class ReservationRequest
    {
        public int PointId { get; set; }
        public DateTime StartTime { get; set; }
        public int DurationMinutes { get; set; } = 60; // Default 1 hour
    }
}
