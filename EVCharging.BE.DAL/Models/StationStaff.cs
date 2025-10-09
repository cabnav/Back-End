using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class StationStaff
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int assignment_id { get; set; }

        [Required]
        public int staff_id { get; set; }

        [Required]
        public int station_id { get; set; }

        public DateTime shift_start { get; set; }

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