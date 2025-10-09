using EVCharging.BE.DAL.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class ChargingPoint
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int point_id { get; set; }

        [Required]
        public int station_id { get; set; }

        [Required]
        [StringLength(20)]
        public string connector_type { get; set; } // CCS, CHAdeMO, AC

        public int power_output { get; set; } // kW

        [Column("price_per_kwh", TypeName = "decimal(8,2)")]
        public decimal price_per_kwh { get; set; }

        [StringLength(20)]
        public string status { get; set; } = "available"; // available, in_use, offline, reserved, maintenance

        [StringLength(100)]
        public string qr_code { get; set; }

        public float current_power { get; set; } = 0; // kW

        public DateTime? last_maintenance { get; set; }

        // Navigation properties
        [ForeignKey("station_id")]
        public virtual ChargingStation ChargingStation { get; set; }

        public virtual ICollection<Reservation> Reservations { get; set; }
        public virtual ICollection<ChargingSession> ChargingSessions { get; set; }
    }
}