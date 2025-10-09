using EVCharging.BE.DAL.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class Reservation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int reservation_id { get; set; }

        [Required]
        public int driver_id { get; set; }

        [Required]
        public int point_id { get; set; }

        public DateTime start_time { get; set; }

        public DateTime end_time { get; set; }

        [StringLength(20)]
        public string status { get; set; } = "booked"; // booked, cancelled, completed, no_show

        [Column("created_at")]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime updated_at { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("driver_id")]
        public virtual DriverProfile DriverProfile { get; set; }

        [ForeignKey("point_id")]
        public virtual ChargingPoint ChargingPoint { get; set; }

        public virtual ICollection<Payment> Payments { get; set; }
    }
}