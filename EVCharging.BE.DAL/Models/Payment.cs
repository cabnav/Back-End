using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("Payment")]
    public class Payment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("payment_id")]
        public int payment_id { get; set; }

        [Required]
        [Column("user_id")]
        public int user_id { get; set; }

        [Column("session_id")]
        public int? session_id { get; set; }

        [Column("reservation_id")]
        public int? reservation_id { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal amount { get; set; }

        [StringLength(30)]
        [Column("payment_method")]
        public string payment_method { get; set; } // wallet, credit_card, corporate_billing, cash

        [StringLength(20)]
        [Column("payment_status")]
        public string payment_status { get; set; } = "pending"; // pending, success, failed, refunded

        [StringLength(50)]
        [Column("invoice_number")]
        public string invoice_number { get; set; }

        [Column("created_at")]
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