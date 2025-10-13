using System;

namespace EVCharging.BE.Common.DTOs.Users
{
    public class UserDTO
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public decimal? WalletBalance { get; set; }
        public string? BillingType { get; set; }
        public string? MembershipTier { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}