using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.DAL.Models
{
    public class CorporateAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int corporate_id { get; set; }

        [Required]
        [StringLength(200)]
        public string company_name { get; set; }

        [StringLength(50)]
        public string tax_code { get; set; }

        [StringLength(100)]
        public string contact_person { get; set; }

        [EmailAddress]
        [StringLength(255)]
        public string contact_email { get; set; }

        [StringLength(20)]
        public string billing_type { get; set; } // prepaid, postpaid

        [Column("credit_limit", TypeName = "decimal(10,2)")]
        public decimal credit_limit { get; set; } = 0;

        [Required]
        public int admin_user_id { get; set; }

        [StringLength(20)]
        public string status { get; set; } = "active"; // active, suspended

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("admin_user_id")]
        public virtual User AdminUser { get; set; }

        public virtual ICollection<DriverProfile> DriverProfiles { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; }
    }
}
