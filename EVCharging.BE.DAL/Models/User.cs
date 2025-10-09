using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{


    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    namespace EVChargingManagement.Models
    {
        public class User
        {
            [Key]
            [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
            public int user_id { get; set; }

            [Required]
            [StringLength(100)]
            public string name { get; set; }

            [Required]
            [EmailAddress]
            [StringLength(255)]
            public string email { get; set; }

            [Required]
            [StringLength(255)]
            public string password { get; set; }

            [StringLength(20)]
            public string phone { get; set; }

            [Required]
            [StringLength(20)]
            public string role { get; set; } // driver, staff, admin

            [Column(TypeName = "decimal(10,2)")]
            public decimal wallet_balance { get; set; } = 0;

            [StringLength(20)]
            public string billing_type { get; set; } // prepaid, postpaid

            [StringLength(50)]
            public string membership_tier { get; set; } // standard, vip, corporate

            public DateTime created_at { get; set; } = DateTime.UtcNow;

            // Navigation properties
            public virtual DriverProfile DriverProfile { get; set; }
            public virtual ICollection<Payment> Payments { get; set; }
            public virtual ICollection<WalletTransaction> WalletTransactions { get; set; }
            public virtual ICollection<Subscription> Subscriptions { get; set; }
        }
    }
}
