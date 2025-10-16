using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Corporates
{
    public class CorporateAccountCreateRequest
    {
        public string CompanyName { get; set; } = "";
        public string? TaxCode { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactEmail { get; set; }
        public string BillingType { get; set; } = "postpaid"; // 'prepaid' | 'postpaid'
        public decimal? CreditLimit { get; set; } = 0m;
        public int AdminUserId { get; set; }                   // NOT NULL trong DB
        public string? Status { get; set; } = "active";        // 'active' | 'suspended'
    }

    public class CorporateAccountDTO
    {
        public int CorporateId { get; set; }
        public string CompanyName { get; set; } = "";
        public string? ContactPerson { get; set; }
        public string? ContactEmail { get; set; }
        public string BillingType { get; set; } = "";
        public decimal CreditLimit { get; set; }
        public string Status { get; set; } = "";
        public int AdminUserId { get; set; }
        public DateTime? CreatedAt { get; set; }

        // Thống kê: số tài xế thuộc DN (DriverProfile.CorporateId = CorporateId)
        public int DriverCount { get; set; }
    }
}

