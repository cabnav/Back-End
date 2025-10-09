using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Common.DTOs.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Payments
{
    public class PaymentDTO
    {
        public int PaymentId { get; set; }
        public int UserId { get; set; }
        public int? SessionId { get; set; }
        public int? ReservationId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public ChargingSessionDTO ChargingSession { get; set; }
        public ReservationDTO Reservation { get; set; }
    }
}
