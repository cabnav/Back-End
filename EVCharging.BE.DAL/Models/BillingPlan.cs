using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("BillingPlan")]
    public class BillingPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("plan_id")]
        public int plan_id { get; set; }

        [Required]
        [StringLength(100)]
        [Column("plan_name")]
        public string plan_name { get; set; }

        [Column("subscription_fee", TypeName = "decimal(10,2)")]
        public decimal subscription_fee { get; set; } = 0;

        [StringLength(20)]
        [Column("billing_cycle")]
        public string billing_cycle { get; set; } // monthly, quarterly

        [StringLength(20)]
        [Column("payment_terms")]
        public string payment_terms { get; set; } // 15 days, 30 days

        [Column("credit_limit", TypeName = "decimal(10,2)")]
        public decimal credit_limit { get; set; } = 0;
    }
}