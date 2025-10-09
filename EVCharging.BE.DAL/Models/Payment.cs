using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class Payment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int payment_id { get; set; }

        [Required]
        public int user_id { get; set; }

        public int? session_id { get; set; }

        public int? reservation_id { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal amount { get; set; }

        [StringLength(30)]
        public string payment_method { get; set; } // wallet, credit_card, corporate_billing, cash

        [StringLength(20)]
        public string payment_status { get; set; } = "pending"; // pending, success, failed, refunded

        [StringLength(50)]
        public string invoice_number { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("user_id")]
        public virtual User User { get; set; }

        [ForeignKey("session_id")]
        public virtual ChargingSession ChargingSession { get; set; }

        [ForeignKey("reservation_id")]
        public virtual Reservation Reservation { get; set; }
    }
}