using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Entities
{
    public partial class Reservation
    {
      
        [Column("reservation_code")]
        public string? ReservationCode { get; set; }

        // (Tuỳ chọn) Constructor đặt giá trị mặc định an toàn
        public Reservation()
        {
            Status ??= "booked";
            CreatedAt ??= DateTime.UtcNow;
            UpdatedAt ??= DateTime.UtcNow;
        }
    }
}
