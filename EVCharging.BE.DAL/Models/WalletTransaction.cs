using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("WalletTransaction")]
    public class WalletTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("transaction_id")]
        public int transaction_id { get; set; }

        [Required]
        [Column("user_id")]
        public int user_id { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal amount { get; set; }

        [StringLength(20)]
        [Column("transaction_type")]
        public string transaction_type { get; set; } // top_up, payment, refund, reward, withdrawal

        [StringLength(255)]
        public string description { get; set; }

        [Column("balance_after", TypeName = "decimal(10,2)")]
        public decimal balance_after { get; set; }

        [Column("reference_id")]
        public int? reference_id { get; set; } // payment_id, etc.

        [Column("created_at")]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("user_id")]
        public virtual User User { get; set; }
    }
}