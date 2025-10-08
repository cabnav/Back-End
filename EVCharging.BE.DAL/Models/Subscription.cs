using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("Subscription")]
    public class Subscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("subscription_id")]
        public int subscription_id { get; set; }

        [Column("user_id")]
        public int? user_id { get; set; }

        [Column("corporate_id")]
        public int? corporate_id { get; set; }

        [Required]
        [Column("plan_id")]
        public int plan_id { get; set; }

        [Column("start_date")]
        public DateTime start_date { get; set; }

        [Column("end_date")]
        public DateTime end_date { get; set; }

        [StringLength(20)]
        public string status { get; set; } = "active"; // active, expired, cancelled

        [Column("auto_renew")]
        public bool auto_renew { get; set; } = false;

        // Navigation properties
        [ForeignKey("user_id")]
        public virtual User User { get; set; }

        [ForeignKey("corporate_id")]
        public virtual CorporateAccount CorporateAccount { get; set; }

        [ForeignKey("plan_id")]
        public virtual PricingPlan PricingPlan { get; set; }
    }
}