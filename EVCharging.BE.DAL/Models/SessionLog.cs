using EVCharging.BE.DAL.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("SessionLog")]
    public class SessionLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("log_id")]
        public int log_id { get; set; }

        [Required]
        [Column("session_id")]
        public int session_id { get; set; }

        [Range(0, 100)]
        [Column("soc_percentage")]
        public int soc_percentage { get; set; } // %

        [Column("current_power", TypeName = "decimal(8,2)")]
        public decimal current_power { get; set; } = 0; // kW

        [Column(TypeName = "decimal(8,2)")]
        public decimal voltage { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal temperature { get; set; } = 0;

        [Column("log_time")]
        public DateTime log_time { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("session_id")]
        public virtual ChargingSession ChargingSession { get; set; }
    }
}