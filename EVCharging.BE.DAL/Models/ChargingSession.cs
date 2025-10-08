using EVCharging.BE.DAL.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("ChargingSession")]
    public class ChargingSession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("session_id")]
        public int session_id { get; set; }

        [Required]
        [Column("driver_id")]
        public int driver_id { get; set; }

        [Required]
        [Column("point_id")]
        public int point_id { get; set; }

        [Column("start_time")]
        public DateTime start_time { get; set; }

        [Column("end_time")]
        public DateTime? end_time { get; set; }

        [Range(0, 100)]
        [Column("initial_soc")]
        public int initial_soc { get; set; } // State of Charge ban đầu (%)

        [Range(0, 100)]
        [Column("final_soc")]
        public int? final_soc { get; set; } // State of Charge cuối cùng (%)

        [Column("energy_used", TypeName = "decimal(8,2)")]
        public decimal energy_used { get; set; } = 0; // kWh

        [Column("duration_minutes")]
        public int duration_minutes { get; set; } = 0;

        [Column("cost_before_discount", TypeName = "decimal(10,2)")]
        public decimal cost_before_discount { get; set; } = 0;

        [Column("applied_discount", TypeName = "decimal(10,2)")]
        public decimal applied_discount { get; set; } = 0;

        [Column("final_cost", TypeName = "decimal(10,2)")]
        public decimal final_cost { get; set; } = 0;

        [StringLength(20)]
        public string status { get; set; } = "in_progress"; // in_progress, completed, interrupted, cancelled

        // Navigation properties
        [ForeignKey("driver_id")]
        public virtual DriverProfile DriverProfile { get; set; }

        [ForeignKey("point_id")]
        public virtual ChargingPoint ChargingPoint { get; set; }

        public virtual ICollection<Payment> Payments { get; set; }
        public virtual ICollection<SessionLog> SessionLogs { get; set; }
    }
}