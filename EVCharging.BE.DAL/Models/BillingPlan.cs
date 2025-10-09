using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class BillingPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int plan_id { get; set; }

        [Required]
        [StringLength(100)]
        public string plan_name { get; set; }

        [Column("subscription_fee", TypeName = "decimal(10,2)")]
        public decimal subscription_fee { get; set; } = 0;

        [StringLength(20)]
        public string billing_cycle { get; set; } // monthly, quarterly

        [StringLength(20)]
        public string payment_terms { get; set; } // 15 days, 30 days

        [Column("credit_limit", TypeName = "decimal(10,2)")]
        public decimal credit_limit { get; set; } = 0;
    }
}