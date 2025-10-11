using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Users
{
    public class UserDTO
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        public decimal WalletBalance { get; set; }
        public string BillingType { get; set; }
        public string MembershipTier { get; set; }
        public DateTime CreatedAt { get; set; }
        public DriverProfileDTO DriverProfile { get; set; }
    }
}
