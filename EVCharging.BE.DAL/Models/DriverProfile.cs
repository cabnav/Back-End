using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class DriverProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int driver_id { get; set; }

        [Required]
        public int user_id { get; set; }

        [StringLength(50)]
        public string license_number { get; set; }

        [StringLength(100)]
        public string vehicle_model { get; set; }

        [StringLength(20)]
        public string vehicle_plate { get; set; }

        public int battery_capacity { get; set; } // kWh

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