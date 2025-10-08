using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("Notification")]
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("notification_id")]
        public int notification_id { get; set; }

        [Required]
        [Column("user_id")]
        public int user_id { get; set; }

        [Required]
        [StringLength(200)]
        public string title { get; set; }

        public string message { get; set; }

        [StringLength(50)]
        public string type { get; set; } // charging_complete, reservation_reminder, promotion, system_alert

        [Column("is_read")]
        public bool is_read { get; set; } = false;

        [Column("related_id")]
        public int? related_id { get; set; } // session_id, reservation_id, etc.

        [Column("created_at")]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("user_id")]
        public virtual User User { get; set; }
    }
}