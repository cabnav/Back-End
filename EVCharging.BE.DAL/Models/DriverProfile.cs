using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("DriverProfile")]
    public class DriverProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("driver_id")]
        public int driver_id { get; set; }

        [Required]
        [Column("user_id")]
        public int user_id { get; set; }

        [StringLength(50)]
        [Column("license_number")]
        public string license_number { get; set; }

        [StringLength(100)]
        [Column("vehicle_model")]
        public string vehicle_model { get; set; }

        [StringLength(20)]
        [Column("vehicle_plate")]
        public string vehicle_plate { get; set; }

        [Column("battery_capacity")]
        public int battery_capacity { get; set; } // kWh

        [Column("corporate_id")]
        public int? corporate_id { get; set; }

        // Navigation properties
        [ForeignKey("user_id")]
        public virtual User User { get; set; }

        [ForeignKey("corporate_id")]
        public virtual CorporateAccount CorporateAccount { get; set; }

        public virtual ICollection<Reservation> Reservations { get; set; }
        public virtual ICollection<ChargingSession> ChargingSessions { get; set; }
    }
}