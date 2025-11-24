using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Reservations
{
    public class ReservationDTO
    {

        public int ReservationId { get; set; }
        public int DriverId { get; set; }
         public string? StationName { get; set; }
        public string? StationAddress { get; set; }
        public int PointId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ChargingPointDTO ChargingPoint { get; set; }
        public UserDTO Driver { get; set; }
        public string? ReservationCode { get; set; }
        
        /// <summary>
        /// Trạng thái thanh toán tiền cọc: "success", "pending", "failed", hoặc null (chưa thanh toán)
        /// </summary>
        public string? DepositPaymentStatus { get; set; }

    }
}
