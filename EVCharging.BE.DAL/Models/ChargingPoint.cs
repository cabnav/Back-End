using EVCharging.BE.DAL.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("ChargingPoint")]
    public class ChargingPoint
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("point_id")]
        public int point_id { get; set; }

        [Required]
        [Column("station_id")]
        public int station_id { get; set; }

        [Required]
        [StringLength(20)]
        [Column("connector_type")]
        public string connector_type { get; set; } // CCS, CHAdeMO, AC

        [Column("power_output")]
        public int power_output { get; set; } // kW

        [Column("price_per_kwh", TypeName = "decimal(8,2)")]
        public decimal price_per_kwh { get; set; }

        [StringLength(20)]
        public string status { get; set; } = "available"; // available, in_use, offline, reserved, maintenance

        [StringLength(100)]
        [Column("qr_code")]
        public string qr_code { get; set; }

        [Column("current_power")]
        public float current_power { get; set; } = 0; // kW

        [Column("last_maintenance")]
        public DateTime? last_maintenance { get; set; }

        // Navigation properties
        [ForeignKey("station_id")]
        public virtual ChargingStation ChargingStation { get; set; }

        public virtual ICollection<Reservation> Reservations { get; set; }
        public virtual ICollection<ChargingSession> ChargingSessions { get; set; }
    }
}