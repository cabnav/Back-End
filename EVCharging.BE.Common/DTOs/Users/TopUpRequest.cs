using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Users
{
    public class TopUpRequest
    {
        [Required]
        [Range(0.01, 1_000_000_000, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        public string? Description { get; set; }
    }
}

