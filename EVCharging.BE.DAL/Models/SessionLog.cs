using EVCharging.BE.DAL.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class SessionLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int log_id { get; set; }

        [Required]
        public int session_id { get; set; }

        [Range(0, 100)]
        public int soc_percentage { get; set; } // %

        [Column("current_power", TypeName = "decimal(8,2)")]
        public decimal current_power { get; set; } = 0; // kW

        [Column(TypeName = "decimal(8,2)")]
        public decimal voltage { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal temperature { get; set; } = 0;

        public DateTime log_time { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("session_id")]
        public virtual ChargingSession ChargingSession { get; set; }
    }
}