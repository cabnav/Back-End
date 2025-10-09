using EVCharging.BE.DAL.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("ChargingStation")]
    public class ChargingStation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int station_id { get; set; }

        [Required]
        [StringLength(100)]
        public string name { get; set; }

        [Required]
        [StringLength(500)]
        public string address { get; set; }

        public double latitude { get; set; }
        public double longitude { get; set; }

        [StringLength(100)]
        [Column("operator")]
        public string operator_name { get; set; } // Renamed from 'operator' to 'operator_name'

        [StringLength(20)]
        public string status { get; set; } = "active"; // active, maintenance, inactive

        public int total_points { get; set; } = 0;

        public int available_points { get; set; } = 0;

        // Navigation properties
        public virtual ICollection<ChargingPoint> ChargingPoints { get; set; }
        public virtual ICollection<StationStaff> StationStaffs { get; set; }
    }
}