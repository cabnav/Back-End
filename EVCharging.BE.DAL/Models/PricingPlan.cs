using EVCharging.BE.DAL.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class PricingPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int plan_id { get; set; }

        [Required]
        [StringLength(100)]
        public string name { get; set; }

        [StringLength(20)]
        public string plan_type { get; set; } // pay_per_use, subscription, corporate

        public string description { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal price { get; set; }

        [StringLength(20)]
        public string billing_cycle { get; set; } // monthly, yearly

        [Column("discount_rate", TypeName = "decimal(5,2)")]
        public decimal discount_rate { get; set; } = 0;

        [StringLength(20)]
        public string target_audience { get; set; } // individual, corporate

        public string benefits { get; set; }

        public bool is_active { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Subscription> Subscriptions { get; set; }
    }
}