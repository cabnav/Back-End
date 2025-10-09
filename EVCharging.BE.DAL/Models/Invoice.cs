using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class Invoice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int invoice_id { get; set; }

        [Required]
        [StringLength(50)]
        public string invoice_number { get; set; }

        public int? user_id { get; set; }

        public int? corporate_id { get; set; }

        [Required]
        public DateTime billing_period_start { get; set; }

        [Required]
        public DateTime billing_period_end { get; set; }

        [Column("total_amount", TypeName = "decimal(10,2)")]
        public decimal total_amount { get; set; }

        [StringLength(20)]
        public string status { get; set; } = "draft"; // draft, pending, paid, overdue, cancelled

        [Required]
        public DateTime due_date { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        public DateTime? paid_at { get; set; }

        // Navigation properties
        [ForeignKey("user_id")]
        public virtual User User { get; set; }

        [ForeignKey("corporate_id")]
        public virtual CorporateAccount CorporateAccount { get; set; }

        public virtual ICollection<InvoiceItem> InvoiceItems { get; set; }
    }
}