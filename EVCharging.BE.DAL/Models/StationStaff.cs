using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("StationStaff")]
    public class StationStaff
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("assignment_id")]
        public int assignment_id { get; set; }

        [Required]
        [Column("staff_id")]
        public int staff_id { get; set; }

        [Required]
        [Column("station_id")]
        public int station_id { get; set; }

        [Column("shift_start")]
        public DateTime shift_start { get; set; }

        [Column("shift_end")]
        public DateTime shift_end { get; set; }

        [StringLength(20)]
        public string status { get; set; } = "active"; // active, inactive

        // Navigation properties
        [ForeignKey("staff_id")]
        public virtual User Staff { get; set; }

        [ForeignKey("station_id")]
        public virtual ChargingStation ChargingStation { get; set; }
    }
}