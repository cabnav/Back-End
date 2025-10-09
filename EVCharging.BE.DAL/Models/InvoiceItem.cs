using EVCharging.BE.DAL.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class InvoiceItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int item_id { get; set; }

        [Required]
        public int invoice_id { get; set; }

        public int? session_id { get; set; }

        [StringLength(255)]
        public string description { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal quantity { get; set; } = 0; // kWh

        [Column("unit_price", TypeName = "decimal(8,2)")]
        public decimal unit_price { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal amount { get; set; } = 0;

        // Navigation properties
        [ForeignKey("invoice_id")]
        public virtual Invoice Invoice { get; set; }

        [ForeignKey("session_id")]
        public virtual ChargingSession ChargingSession { get; set; }
    }
}